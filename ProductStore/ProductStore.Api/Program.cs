using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using productStore.Api.Data;
using productStore.Api.Endpoints;
using ProductStore.Api.Endpoints;
using ProductStore.Api.Hubs;
using ProductStore.Api.Models;
using ProductStore.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- services ----------

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing. Set it with dotnet user-secrets, or the Railway variable Jwt__Key.");

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
builder.Services.AddSingleton<EmailNotificationQueue>();
builder.Services.AddHostedService<EmailNotificationWorker>();
builder.Services.AddSingleton<SmsNotificationQueue>();
builder.Services.AddHostedService<SmsNotificationWorker>();
builder.Services.AddSingleton<OrderNotifications>();

var smsProvider = builder.Configuration["Sms:Provider"]?.Trim();
if (string.Equals(smsProvider, "TextBelt", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<ISmsSender, TextBeltSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(10));
else if (string.Equals(smsProvider, "Vonage", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<ISmsSender, VonageSmsSender>(c => c.Timeout = TimeSpan.FromSeconds(10));
else
    builder.Services.AddSingleton<ISmsSender, DisabledSmsSender>();

// Public checkout is limited because it creates records and can queue notifications.
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

// Railway puts a proxy in front of the app: use the real visitor address
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.AddProductStoreDb();

var app = builder.Build();

var emailUser = builder.Configuration["Email:Username"];
var emailPassword = builder.Configuration["Email:Password"];
var emailEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["Email:Host"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["Email:FromAddress"] ?? emailUser)
    && (string.IsNullOrWhiteSpace(emailUser) || !string.IsNullOrWhiteSpace(emailPassword));
app.Logger.LogInformation(
    "Email notifications are {State}.",
    emailEnabled ? "configured" : "not configured; set Email__Host, Email__FromAddress, Email__Username and Email__Password");

// ---------- database: tables, starting lists, owner account ----------

app.MigrateDb();
await app.SeedStoredImagesAsync();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();

    // remove the old account that was created with a typed-in password and the unsupported role "Admin"
    var stray = await db.AdminUsers.Where(u => u.Role != "Owner" && u.Role != "Viewer").ToListAsync();
    if (stray.Count > 0)
    {
        db.AdminUsers.RemoveRange(stray);
        await db.SaveChangesAsync();
    }

    // the one owner account comes from Admin:Username and Admin:Password (Railway: Admin__Username, Admin__Password)
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
        else app.Logger.LogWarning("No owner account exists. Set Admin:Username and Admin:Password (Railway: Admin__Username and Admin__Password).");
    }
}

// ---------- pipeline: the order matters ----------

app.UseForwardedHeaders();
if (app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UsePathBase("/api");     // /api/products and /products reach the same endpoint
app.UseRouting();            // must come right after UsePathBase
app.UseDefaultFiles();       // serves index.html at "/"
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

// ---------- endpoints ----------

app.MapHub<AdminHub>("/hubs/admin");
app.MapGet("/status", () => Results.Ok(new { service = "ProductStore API", status = "running" }));
app.MapGet("/notifications/options", (IConfiguration config) =>
{
    var emailUser = config["Email:Username"];
    var emailPassword = config["Email:Password"];
    var emailEnabled = !string.IsNullOrWhiteSpace(config["Email:Host"])
        && !string.IsNullOrWhiteSpace(config["Email:FromAddress"] ?? emailUser)
        && (string.IsNullOrWhiteSpace(emailUser) || !string.IsNullOrWhiteSpace(emailPassword));

    return Results.Ok(new
    {
        smsEnabled = SmsSettings.IsEnabled(config),
        emailEnabled
    });
});

app.MapAuthEndpoints();
app.MapAdminsEndpoints();
app.MapProductsEndpoints();
app.MapGenreEndpPoints();
app.MapColorsEndpPoints();
app.MapSizesEndpPoints();
app.MapCustomersEndpoints();
app.MapCommandsEndpoints();
app.MapCheckoutEndpoints();

app.MapPost("/upload", async (
    IFormFile file,
    ProductStoreContext db,
    CancellationToken cancellationToken) =>
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (file.Length == 0 || file.Length > 5_000_000 || ImageFiles.ContentTypeFor(file.FileName) is null)
        return Results.BadRequest("Upload a JPG, PNG or WebP image under 5 MB.");

    await using var memory = new MemoryStream();
    await file.CopyToAsync(memory, cancellationToken);
    var data = memory.ToArray();
    var contentType = ImageFiles.DetectContentType(data, file.FileName);
    if (contentType is null)
        return Results.BadRequest("That file is not a valid JPG, PNG or WebP image.");

    var name = $"{Guid.NewGuid():N}{extension}";
    db.StoredImages.Add(new StoredImage
    {
        FileName = name,
        ContentType = contentType,
        Data = data
    });
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { url = $"/uploads/{name}", persistent = true });
}).DisableAntiforgery().RequireAuthorization("Owner");

app.MapGet("/uploads/{fileName}", async (
    string fileName,
    ProductStoreContext db,
    IWebHostEnvironment env,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        return Results.NotFound();

    var image = await db.StoredImages.AsNoTracking()
        .FirstOrDefaultAsync(i => i.FileName == fileName, cancellationToken);
    if (image is not null)
    {
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Results.File(image.Data, image.ContentType, fileName, enableRangeProcessing: true);
    }

    var existingFile = Path.Combine(env.ContentRootPath, "wwwroot", "uploads", fileName);
    if (File.Exists(existingFile))
        return Results.File(existingFile, ImageFiles.ContentTypeFor(fileName) ?? "application/octet-stream");

    var placeholder = Path.Combine(env.ContentRootPath, "wwwroot", "placeholder.svg");
    return Results.File(placeholder, "image/svg+xml");
}).AllowAnonymous();

app.MapFallbackToFile("index.html");     // keep last: lets /cart and /admin open when the page is refreshed

app.Run();