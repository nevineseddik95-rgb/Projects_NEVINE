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
                hasher.HashPassword(new AdminUser(), req.Password ?? "");
                return Results.Unauthorized();
            }

            var verify = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "");
            if (verify == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            if (verify == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = hasher.HashPassword(user, req.Password!);
                await db.SaveChangesAsync();
            }

            var jwtKey = config["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "Jwt:Key is not configured. Set the Jwt__Key environment variable in Railway.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = config["Jwt:Issuer"],
                Audience = config["Jwt:Audience"],
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role ?? "Admin")
                }),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };
            var token = new JsonWebTokenHandler().CreateToken(descriptor);
            return Results.Ok(new { token, role = user.Role ?? "Admin", username = user.Username });
        }).RequireRateLimiting("login");
    }
}