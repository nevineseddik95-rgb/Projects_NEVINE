using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using ProductStore.Api.Services;
using productStore.Api.Endpoints;
using ProductStore.Api.Endpoints;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using ProductStore.Api.Hubs;
using ProductStore.Api.Models;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing. Set it with dotnet user-secrets.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true
        };
        // the live connection sends the token in the address
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Owner", p => p.RequireRole("Owner"))                 // can change everything
    .AddPolicy("Staff", p => p.RequireRole("Owner", "Viewer"));      // can look at everything

builder.Services.AddSignalR();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHttpClient<ISmsSender, TwilioSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(10));

// the text message costs money, so the public checkout is limited per visitor
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("checkout", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

builder.AddProductStoreDb();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddCors(o => o.AddPolicy("angular", p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

var app = builder.Build();
// Ensure DB is migrated and has at least one admin user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
    db.Database.Migrate();   // creates tables if missing

    if (!db.AdminUsers.Any())
    {
        var hasher = new PasswordHasher<AdminUser>();
        var admin = new AdminUser
        {
            Username = "admin",
            Role = "Admin"
        };
        admin.PasswordHash = hasher.HashPassword(admin, "ChangeMe123!");

        db.AdminUsers.Add(admin);
        db.SaveChanges();
    }
}
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
    if (!await db.AdminUsers.AnyAsync(u => u.Role == "Owner"))
    {
        var ownerName = builder.Configuration["Admin:Username"]?.Trim().ToLowerInvariant();
        var ownerPass = builder.Configuration["Admin:Password"];
        if (!string.IsNullOrEmpty(ownerName) && !string.IsNullOrEmpty(ownerPass))
        {
            var owner = new AdminUser { Username = ownerName, Role = "Owner" };
            owner.PasswordHash = new PasswordHasher<AdminUser>().HashPassword(owner, ownerPass);
            db.AdminUsers.Add(owner);
            await db.SaveChangesAsync();
        }
        else app.Logger.LogWarning("No owner account exists. Set Admin:Username and Admin:Password with dotnet user-secrets.");
    }
}
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Viewers can only look. Every change needs the owner, except the customer checkout, the login and the live connection.
var openWrites = new[] { "/auth/login", "/checkout", "/hubs" };
app.Use(async (ctx, next) =>
{
    var method = ctx.Request.Method;
    var isWrite = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
                  HttpMethods.IsDelete(method) || HttpMethods.IsPatch(method);
    var path = ctx.Request.Path;

    if (isWrite && !openWrites.Any(p => path.StartsWithSegments(p)))
    {
        if (ctx.User.Identity?.IsAuthenticated != true) { ctx.Response.StatusCode = 401; return; }
        if (!ctx.User.IsInRole("Owner")) { ctx.Response.StatusCode = 403; return; }
    }

    await next();

    // tell the open admin pages that something changed, so their lists refresh by themselves
    if (isWrite && ctx.Response.StatusCode is >= 200 and < 300)
    {
        var first = (path.Value ?? "").Trim('/').Split('/')[0].ToLowerInvariant();
        string[] changed = first switch
        {
            "checkout" => new[] { "commands", "customers" },
            "" or "auth" or "hubs" or "upload" => Array.Empty<string>(),
            _ => new[] { first }
        };
        if (changed.Length > 0)
        {
            var hub = ctx.RequestServices.GetRequiredService<IHubContext<AdminHub>>();
            await hub.Clients.All.SendAsync("changed", changed);
        }
    }
});

app.MapHub<AdminHub>("/hubs/admin");
app.MapAuthEndpoints();
app.MapAdminsEndpoints();
// ...your other app.Map...Endpoints() lines
app.UseStaticFiles();   // serves wwwroot, so /uploads/x.jpg is reachable
app.MapPost("/set-image/{id}", async (Guid id, string url, ProductStoreContext db) =>
{
    var p = await db.Products.FindAsync(id);
    if (p is null) return Results.NotFound();
    p.ImageUrl = url;
    await db.SaveChangesAsync();
    return Results.Ok();
});
app.MapPost("/upload", async (IFormFile file, IWebHostEnvironment env) =>
{
    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (file.Length == 0 || file.Length > 5_000_000 || !allowed.Contains(ext))
        return Results.BadRequest("Upload a JPG, PNG or WebP image under 5 MB.");

    var folder = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
    Directory.CreateDirectory(folder);
    var name = $"{Guid.NewGuid():N}{ext}";
    await using var stream = File.Create(Path.Combine(folder, name));
    await file.CopyToAsync(stream);

    return Results.Ok(new { url = $"/uploads/{name}" });
}).DisableAntiforgery().RequireAuthorization();

app.UseCors("angular");   // before UseAuthorization()
app.MapProductsEndpoints();
app.MapGenreEndpPoints();
app.MapColorsEndpPoints();
app.MapSizesEndpPoints();
app.MapCustomersEndpoints();
app.MapCommandsEndpoints();
app.MapCheckoutEndpoints();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MigrateDb();
app.MapGet("/", () => Results.Ok(new { 
    service = "ProductStore API", 
    status = "running",
    endpoints = new[] { "/auth/login", "/swagger" }
}));
app.Run();
