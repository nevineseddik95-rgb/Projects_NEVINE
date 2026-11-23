using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using ProductStore.Api.Models;

namespace ProductStore.Api.Endpoints;

public record SaveAdmin(string Username, string? Password);

public static class AdminsEndpoints
{
    public static void MapAdminsEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/admins").RequireAuthorization("Owner");
        var hasher = new PasswordHasher<AdminUser>();

        g.MapGet("/", async (ProductStoreContext db) =>
            await db.AdminUsers.AsNoTracking().OrderBy(u => u.CreatedAt)
                .Select(u => new { u.Id, u.Username, u.Role, u.CreatedAt }).ToListAsync());

        // new accounts are always view-only; there is only ever one owner
        g.MapPost("/", async (SaveAdmin dto, ProductStoreContext db) =>
        {
            var name = (dto.Username ?? "").Trim().ToLowerInvariant();
            if (name.Length < 3 || name.Length > 100) return Results.BadRequest("The username needs 3 to 100 characters.");
            if ((dto.Password ?? "").Length < 8) return Results.BadRequest("The password needs at least 8 characters.");
            if (await db.AdminUsers.AnyAsync(u => u.Username == name)) return Results.BadRequest("That username is already taken.");

            var user = new AdminUser { Username = name, Role = "Viewer" };
            user.PasswordHash = hasher.HashPassword(user, dto.Password!);
            db.AdminUsers.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/admins/{user.Id}", new { user.Id, user.Username, user.Role, user.CreatedAt });
        });

        g.MapPut("/{id}", async (Guid id, SaveAdmin dto, ProductStoreContext db) =>
        {
            var user = await db.AdminUsers.FindAsync(id);
            if (user is null) return Results.NotFound();

            if (!string.IsNullOrEmpty(dto.Password))
            {
                if (dto.Password.Length < 8) return Results.BadRequest("The password needs at least 8 characters.");
                user.PasswordHash = hasher.HashPassword(user, dto.Password);
            }

            var name = (dto.Username ?? "").Trim().ToLowerInvariant();
            if (user.Role != "Owner" && name.Length > 0 && name != user.Username)
            {
                if (name.Length < 3 || name.Length > 100) return Results.BadRequest("The username needs 3 to 100 characters.");
                if (await db.AdminUsers.AnyAsync(u => u.Username == name)) return Results.BadRequest("That username is already taken.");
                user.Username = name;
            }
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/{id}", async (Guid id, ProductStoreContext db) =>
        {
            var user = await db.AdminUsers.FindAsync(id);
            if (user is null) return Results.NotFound();
            if (user.Role == "Owner") return Results.BadRequest("The owner account cannot be deleted.");
            db.AdminUsers.Remove(user);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}