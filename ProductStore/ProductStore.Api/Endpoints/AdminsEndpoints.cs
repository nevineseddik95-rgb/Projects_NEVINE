using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using ProductStore.Api.Models;
using ProductStore.Api.Services;

namespace ProductStore.Api.Endpoints;

public record SaveAdmin(string Username, string? Password, string? Phone = null);

public static class AdminsEndpoints
{
    public static void MapAdminsEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/admins").RequireAuthorization("Owner");
        var hasher = new PasswordHasher<AdminUser>();

        g.MapGet("/", async (ProductStoreContext db) =>
            await db.AdminUsers.AsNoTracking().OrderBy(u => u.CreatedAt)
                .Select(u => new { u.Id, u.Username, u.Role, u.Phone, u.CreatedAt }).ToListAsync());

        // new accounts are always view-only; there is only ever one owner
        g.MapPost("/", async (SaveAdmin dto, ProductStoreContext db) =>
        {
            var name = (dto.Username ?? "").Trim().ToLowerInvariant();
            if (name.Length < 3 || name.Length > 100) return Results.BadRequest("The username needs 3 to 100 characters.");
            if ((dto.Password ?? "").Length < 8) return Results.BadRequest("The password needs at least 8 characters.");
            var phone = PhoneNumber.Normalize(dto.Phone);
            if (!string.IsNullOrWhiteSpace(dto.Phone) && phone is null)
                return Results.BadRequest("Enter a valid phone number, including the country code when needed.");
            if (await db.AdminUsers.AnyAsync(u => u.Username == name)) return Results.BadRequest("That username is already taken.");

            var user = new AdminUser { Username = name, Role = "Viewer", Phone = phone };
            user.PasswordHash = hasher.HashPassword(user, dto.Password!);
            db.AdminUsers.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/admins/{user.Id}", new { user.Id, user.Username, user.Role, user.Phone, user.CreatedAt });
        });

        g.MapPut("/{id}", async (Guid id, SaveAdmin dto, ProductStoreContext db) =>
        {
            var user = await db.AdminUsers.FindAsync(id);
            if (user is null) return Results.NotFound();
            if (user.Role == "Owner") return Results.BadRequest("The owner account cannot be edited.");
            var phone = PhoneNumber.Normalize(dto.Phone);
            if (!string.IsNullOrWhiteSpace(dto.Phone) && phone is null)
                return Results.BadRequest("Enter a valid phone number, including the country code when needed.");
            user.Phone = phone;

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

        g.MapDelete("/{id}", async (
            Guid id,
            ProductStoreContext db,
            AdminChangeNotifier realtime,
            CancellationToken cancellationToken) =>
        {
            var user = await db.AdminUsers.FindAsync([id], cancellationToken);
            if (user is null) return Results.NotFound();
            if (user.Role == "Owner")
                return Results.BadRequest("The owner account cannot be deleted.");

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Commands
                .Where(c => c.CreatedByAdminId == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.CreatedByAdminId, (Guid?)null)
                    .SetProperty(c => c.CreatedByAdminName, user.Username)
                    .SetProperty(c => c.CreatedByAdminRole, user.Role)
                    .SetProperty(c => c.CreatedByAdminPhone, user.Phone)
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken);
            db.AdminUsers.Remove(user);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await realtime.NotifyAsync(["admins", "commands", "commissions"], cancellationToken);
            return Results.NoContent();
        });
    }
}