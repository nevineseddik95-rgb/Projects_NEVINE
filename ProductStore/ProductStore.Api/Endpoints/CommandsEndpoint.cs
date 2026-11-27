using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Models;
using ProductStore.Api.Services;

namespace ProductStore.Api.Endpoints;

public record SaveCommand(
    Guid CustomerId,
    Guid ProductId,
    Guid GenreId,
    Guid ColorId,
    Guid? SizeId,
    string DeliveryAddress,
    DateOnly Date,
    decimal Total,
    string? Note,
    string? ImageUrl,
    decimal DepositAmount = 0,
    string? PaymentReference = null,
    string? TrackingNumber = null);

public record ChangeOrderStatus(string Status, string? Note = null, string? TrackingNumber = null);

public static class CommandsEndpoints
{
    public static void MapCommandsEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/commands").RequireAuthorization("Staff");

        g.MapGet("/", async (ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var orders = await (
                from command in db.Commands.AsNoTracking()
                join customer in db.Customers.AsNoTracking() on command.CustomerId equals customer.Id
                orderby command.CreatedAt descending, command.Id descending
                select new
                {
                    command.Id,
                    command.ReferenceNumber,
                    command.CustomerId,
                    command.ProductId,
                    command.Model,
                    command.GenreId,
                    command.ColorId,
                    command.SizeId,
                    command.DeliveryAddress,
                    command.Date,
                    command.Status,
                    command.StatusNote,
                    command.TrackingNumber,
                    command.CreatedAt,
                    command.UpdatedAt,
                    command.StatusUpdatedAt,
                    command.DeliveredAt,
                    command.CancelledAt,
                    command.Total,
                    command.Note,
                    command.ImageUrl,
                    command.DepositAmount,
                    command.PaymentStatus,
                    command.PaymentReference,
                    command.PaymentMethod,
                    customerPhone = customer.Phone,
                    customerName = customer.FullName,
                    customerEmail = customer.Email
                }).ToListAsync(cancellationToken);

            return Results.Ok(orders);
        });

        g.MapGet("/{id}", async (Guid id, ProductStoreContext db, CancellationToken cancellationToken) =>
            await db.Commands.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken) is { } c
                ? Results.Ok(c)
                : Results.NotFound());

        g.MapPost("/", async (SaveCommand dto, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var product = await db.Products.FindAsync([dto.ProductId], cancellationToken);
            if (product is null) return Results.BadRequest("Product not found.");
            if (!await db.Customers.AnyAsync(c => c.Id == dto.CustomerId, cancellationToken))
                return Results.BadRequest("Customer not found.");

            var referenceError = await ValidatePaymentReferenceAsync(db, dto.PaymentReference, null, cancellationToken);
            if (referenceError is not null) return Results.BadRequest(referenceError);

            var command = new Command
            {
                ReferenceNumber = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                Model = product.Model,
                Status = "Pending",
                PaymentStatus = "Unpaid"
            };
            Fill(command, dto);
            if (string.IsNullOrEmpty(command.ImageUrl)) command.ImageUrl = product.ImageUrl;

            db.Commands.Add(command);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/commands/{command.Id}", command);
        });

        g.MapPut("/{id}", async (Guid id, SaveCommand dto, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();

            if (command.ProductId != dto.ProductId)
            {
                var product = await db.Products.FindAsync([dto.ProductId], cancellationToken);
                if (product is null) return Results.BadRequest("Product not found.");
                command.Model = product.Model;
            }
            if (!await db.Customers.AnyAsync(c => c.Id == dto.CustomerId, cancellationToken))
                return Results.BadRequest("Customer not found.");

            var referenceError = await ValidatePaymentReferenceAsync(db, dto.PaymentReference, id, cancellationToken);
            if (referenceError is not null) return Results.BadRequest(referenceError);

            Fill(command, dto);
            command.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        g.MapDelete("/{id}", async (Guid id, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();
            if (command.Status is "Delivered" or "Shipped")
                return Results.BadRequest("Shipped and delivered orders are kept as business records and cannot be deleted.");

            await db.CommandItems.Where(i => i.CommandId == id).ExecuteDeleteAsync(cancellationToken);
            db.Commands.Remove(command);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        g.MapPost("/{id}/status", async (
            Guid id,
            ChangeOrderStatus dto,
            ProductStoreContext db,
            OrderNotifications notifications,
            CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();

            var nextStatus = (dto.Status ?? "").Trim();
            var error = OrderWorkflow.ValidateStatus(command.Status, nextStatus, command.PaymentStatus);
            if (error is not null) return Results.BadRequest(error);

            var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
            var tracking = string.IsNullOrWhiteSpace(dto.TrackingNumber) ? command.TrackingNumber : dto.TrackingNumber.Trim();
            if (note?.Length > 1000) return Results.BadRequest("The status note cannot be longer than 1,000 characters.");
            if (tracking?.Length > 100) return Results.BadRequest("The tracking number cannot be longer than 100 characters.");

            var previousStatus = command.Status;
            command.Status = nextStatus;
            command.StatusNote = note ?? $"Status changed from {previousStatus} to {nextStatus}.";
            command.TrackingNumber = tracking;
            command.StatusUpdatedAt = DateTime.UtcNow;
            command.UpdatedAt = DateTime.UtcNow;
            if (nextStatus == "Delivered") command.DeliveredAt = DateTime.UtcNow;
            if (nextStatus == "Cancelled") command.CancelledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var customer = await db.Customers.FindAsync([command.CustomerId], cancellationToken);
            var notificationResult = customer is null
                ? new NotificationResult(false, false)
                : notifications.QueueStatusChanged(command, customer, previousStatus);

            return Results.Ok(new
            {
                command.Id,
                command.Status,
                command.PaymentStatus,
                command.StatusUpdatedAt,
                emailQueued = notificationResult.EmailQueued,
                smsQueued = notificationResult.SmsQueued
            });
        });

        g.MapPost("/{id}/confirm-payment", async (
            Guid id,
            ProductStoreContext db,
            OrderNotifications notifications,
            CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();
            var error = OrderWorkflow.ValidatePayment(command.PaymentStatus, "Deposit paid");
            if (error is not null) return Results.BadRequest(error);

            command.PaymentStatus = "Deposit paid";
            command.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var customer = await db.Customers.FindAsync([command.CustomerId], cancellationToken);
            var notificationResult = customer is null
                ? new NotificationResult(false, false)
                : notifications.QueuePaymentConfirmed(command, customer, "Deposit confirmed");

            return Results.Ok(new
            {
                command.Id,
                command.Status,
                command.PaymentStatus,
                emailQueued = notificationResult.EmailQueued,
                smsQueued = notificationResult.SmsQueued
            });
        });

        g.MapPost("/{id}/mark-paid-in-full", async (
            Guid id,
            ProductStoreContext db,
            OrderNotifications notifications,
            CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();
            var error = OrderWorkflow.ValidatePayment(command.PaymentStatus, "Paid in full");
            if (error is not null) return Results.BadRequest(error);

            command.PaymentStatus = "Paid in full";
            command.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var customer = await db.Customers.FindAsync([command.CustomerId], cancellationToken);
            var notificationResult = customer is null
                ? new NotificationResult(false, false)
                : notifications.QueuePaymentConfirmed(command, customer, "Payment complete");

            return Results.Ok(new
            {
                command.Id,
                command.Status,
                command.PaymentStatus,
                emailQueued = notificationResult.EmailQueued,
                smsQueued = notificationResult.SmsQueued
            });
        });
    }

    private static void Fill(Command command, SaveCommand dto)
    {
        command.CustomerId = dto.CustomerId;
        command.ProductId = dto.ProductId;
        command.GenreId = dto.GenreId;
        command.ColorId = dto.ColorId;
        command.SizeId = dto.SizeId;
        command.DeliveryAddress = dto.DeliveryAddress;
        command.Date = dto.Date;
        command.Total = dto.Total;
        command.Note = dto.Note;
        command.ImageUrl = dto.ImageUrl;
        command.DepositAmount = dto.DepositAmount;
        command.PaymentReference = dto.PaymentReference?.Trim();
        command.TrackingNumber = dto.TrackingNumber?.Trim();
    }

    private static async Task<string?> ValidatePaymentReferenceAsync(
        ProductStoreContext db,
        string? paymentReference,
        Guid? commandId,
        CancellationToken cancellationToken)
    {
        var reference = paymentReference?.Trim();
        if (string.IsNullOrEmpty(reference)) return null;
        if (reference.Length is < 3 or > 100)
            return "The transaction ID must be between 3 and 100 characters.";
        if (await db.Commands.AnyAsync(c => c.Id != commandId && c.PaymentReference == reference, cancellationToken))
            return "This transaction ID is already linked to another order.";
        return null;
    }
}
