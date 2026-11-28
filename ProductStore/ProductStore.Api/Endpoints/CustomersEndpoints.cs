using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Models;
using ProductStore.Api.Services;

namespace productStore.Api.Endpoints;

public record SaveCustomer(string FullName, string Email, string? Phone, string? Address);

public static class CustomersEndpoints
{
    public static void MapCustomersEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/customers").RequireAuthorization("Staff");

        g.MapGet("/", (ProductStoreContext db) => db.Customers.AsNoTracking().ToListAsync());

        g.MapGet("/{id}", async (Guid id, ProductStoreContext db) =>
            await db.Customers.FindAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        g.MapPost("/", async (SaveCustomer dto, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var name = (dto.FullName ?? "").Trim();
            if (name.Length == 0) return Results.BadRequest("Customer name is required.");
            if (!EmailAddress.TryNormalize(dto.Email, out var email))
                return Results.BadRequest("That email address does not look right.");

            var c = new Customer
            {
                FullName = name,
                Email = email,
                Phone = dto.Phone?.Trim(),
                Address = dto.Address?.Trim()
            };
            db.Customers.Add(c);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/customers/{c.Id}", c);
        });

        g.MapPut("/{id}", async (Guid id, SaveCustomer dto, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var c = await db.Customers.FindAsync([id], cancellationToken);
            if (c is null) return Results.NotFound();

            var name = (dto.FullName ?? "").Trim();
            if (name.Length == 0) return Results.BadRequest("Customer name is required.");
            if (!EmailAddress.TryNormalize(dto.Email, out var email))
                return Results.BadRequest("That email address does not look right.");

            c.FullName = name;
            c.Email = email;
            c.Phone = dto.Phone?.Trim();
            c.Address = dto.Address?.Trim();
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        g.MapDelete("/{id}", async (Guid id, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var c = await db.Customers.FindAsync([id], cancellationToken);
            if (c is null) return Results.NotFound();
            if (await db.Commands.AnyAsync(o => o.CustomerId == id, cancellationToken))
                return Results.Conflict("This customer has orders and cannot be deleted. Edit the customer instead.");
            db.Customers.Remove(c);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        // call when a customer visits, so the admin sees "last visit"
        g.MapPost("/{id}/visit", async (Guid id, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var c = await db.Customers.FindAsync([id], cancellationToken);
            if (c is null) return Results.NotFound();
            c.LastVisit = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }
}
