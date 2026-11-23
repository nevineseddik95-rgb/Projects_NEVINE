using System;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Models;

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

        g.MapPost("/", async (SaveCustomer dto, ProductStoreContext db) =>
        {
            var c = new Customer { FullName = dto.FullName, Email = dto.Email, Phone = dto.Phone, Address = dto.Address };
            db.Customers.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/customers/{c.Id}", c);
        });

        g.MapPut("/{id}", async (Guid id, SaveCustomer dto, ProductStoreContext db) =>
        {
            var c = await db.Customers.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.FullName = dto.FullName; c.Email = dto.Email; c.Phone = dto.Phone; c.Address = dto.Address;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/{id}", async (Guid id, ProductStoreContext db) =>
        {
            var c = await db.Customers.FindAsync(id);
            if (c is null) return Results.NotFound();
            db.Customers.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // call when a customer visits, so the admin sees "last visit"
        g.MapPost("/{id}/visit", async (Guid id, ProductStoreContext db) =>
        {
            var c = await db.Customers.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.LastVisit = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

}
