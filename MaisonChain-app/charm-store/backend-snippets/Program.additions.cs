// Add to Program.cs of your existing .NET Core API.

// 1) CORS (only needed if you do NOT use the Angular dev proxy)
builder.Services.AddCors(o => o.AddPolicy("angular", p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

// ... after var app = builder.Build();
app.UseStaticFiles();            // serves wwwroot/uploads/* so <img src="/uploads/x.jpg"> works
app.UseCors("angular");
app.UseAuthentication();
app.UseAuthorization();

// 2) POST /api/auth/login  ->  { "token": "<JWT>" }   (body: { "username", "password" })
// 3) Mark admin controllers with [Authorize]; leave GET products/genres/colors/sizes public for the storefront.
// 4) Optional: update Customer.LastVisit (DateTime?) from your login/visit endpoint so the admin sees it.
