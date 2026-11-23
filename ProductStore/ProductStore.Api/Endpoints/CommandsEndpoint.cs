using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Models;
using ProductStore.Api.Models;

namespace ProductStore.Api.Endpoints;

// what the admin Edit form sends
public record SaveCommand(Guid CustomerId, Guid ProductId, Guid GenreId, Guid ColorId, Guid? SizeId,
    string DeliveryAddress, DateOnly Date, string Status, decimal Total, string? Note, string? ImageUrl,
    decimal DepositAmount = 0, string PaymentStatus = "Unpaid", string? PaymentReference = null);

public static class CommandsEndpoints
{
    public static void MapCommandsEndpoints(this WebApplication app)
    {
        // once the admin login is set up, add .RequireAuthorization() at the end of this line
        var g = app.MapGroup("/commands").RequireAuthorization("Staff");

        g.MapGet("/", (ProductStoreContext db) =>
            db.Commands.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync());

        g.MapGet("/{id}", async (Guid id, ProductStoreContext db) =>
            await db.Commands.FindAsync(id) is { } c ? Results.Ok(c) : Results.NotFound());

        // admin creates an order by hand
        g.MapPost("/", async (SaveCommand dto, ProductStoreContext db) =>
        {
            var product = await db.Products.FindAsync(dto.ProductId);
            if (product is null) return Results.BadRequest("Product not found.");

            var c = new Command
            {
                ReferenceNumber = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                Model = product.Model
            };
            Fill(c, dto);
            if (string.IsNullOrEmpty(c.ImageUrl)) c.ImageUrl = product.ImageUrl;

            db.Commands.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/commands/{c.Id}", c);
        });

        // admin edits an order
        g.MapPut("/{id}", async (Guid id, SaveCommand dto, ProductStoreContext db) =>
        {
            var c = await db.Commands.FindAsync(id);
            if (c is null) return Results.NotFound();

            if (c.ProductId != dto.ProductId)
            {
                var product = await db.Products.FindAsync(dto.ProductId);
                if (product is null) return Results.BadRequest("Product not found.");
                c.Model = product.Model;
            }
            Fill(c, dto);                       // ReferenceNumber and PaymentMethod never change here

            var paid = c.PaymentStatus is "Deposit paid" or "Paid in full";
            if (!paid && (c.Status is "Processing" or "Shipped" or "Delivered"))
                return Results.BadRequest("Confirm the deposit before processing, shipping or delivering the order.");

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapDelete("/{id}", async (Guid id, ProductStoreContext db) =>
        {
            var c = await db.Commands.FindAsync(id);
            if (c is null) return Results.NotFound();
            await db.CommandItems.Where(i => i.CommandId == id).ExecuteDeleteAsync();
            db.Commands.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // button: the deposit was received
        g.MapPost("/{id}/confirm-payment", async (Guid id, ProductStoreContext db) =>
        {
            var c = await db.Commands.FindAsync(id);
            if (c is null) return Results.NotFound();
            if (c.PaymentStatus is not ("Unpaid" or "Payment submitted"))
                return Results.BadRequest("The deposit is already confirmed.");

            c.PaymentStatus = "Deposit paid";
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id, c.Status, c.PaymentStatus });
        });

        // button: the balance was received too
        g.MapPost("/{id}/mark-paid-in-full", async (Guid id, ProductStoreContext db) =>
        {
            var c = await db.Commands.FindAsync(id);
            if (c is null) return Results.NotFound();
            if (c.PaymentStatus != "Deposit paid") return Results.BadRequest("Confirm the deposit first.");

            c.PaymentStatus = "Paid in full";
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id, c.Status, c.PaymentStatus });
        });
    }

    private static void Fill(Command c, SaveCommand d)
    {
        c.CustomerId = d.CustomerId; c.ProductId = d.ProductId;
        c.GenreId = d.GenreId; c.ColorId = d.ColorId; c.SizeId = d.SizeId;
        c.DeliveryAddress = d.DeliveryAddress; c.Date = d.Date;
        c.Status = d.Status; c.Total = d.Total; c.Note = d.Note; c.ImageUrl = d.ImageUrl;
        c.DepositAmount = d.DepositAmount; c.PaymentStatus = d.PaymentStatus; c.PaymentReference = d.PaymentReference;
    }
}