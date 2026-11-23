using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using productStore.Api.Data;
using ProductStore.Api.Models;

namespace ProductStore.Api.Endpoints;

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (LoginRequest req, ProductStoreContext db, IConfiguration config) =>
        {
            var name = (req.Username ?? "").Trim().ToLowerInvariant();
            var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == name);
            var hasher = new PasswordHasher<AdminUser>();

            if (user is null)
            {
                hasher.HashPassword(new AdminUser(), req.Password ?? "");   // same delay whether the name or the password is wrong
                return Results.Unauthorized();
            }
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = config["Jwt:Issuer"],
                Audience = config["Jwt:Audience"],
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };
            var token = new JsonWebTokenHandler().CreateToken(descriptor);
            return Results.Ok(new { token, role = user.Role, username = user.Username });
        }).RequireRateLimiting("login");
    }
}