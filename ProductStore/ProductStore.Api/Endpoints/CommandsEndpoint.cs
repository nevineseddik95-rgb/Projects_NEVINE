using System.Net;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Models;
using ProductStore.Api.Models;
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

        g.MapGet("/", (ProductStoreContext db, CancellationToken cancellationToken) =>
            db.Commands.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .ToListAsync(cancellationToken));

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
            EmailNotificationQueue emailQueue,
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
            if (customer is not null && !string.IsNullOrWhiteSpace(customer.Email))
            {
                emailQueue.TryEnqueue(new EmailNotification(
                    customer.Email,
                    customer.FullName,
                    $"Order {command.ReferenceNumber} is now {nextStatus}",
                    BuildCustomerStatusEmail(command, customer.FullName)));
            }

            return Results.Ok(new { command.Id, command.Status, command.PaymentStatus, command.StatusUpdatedAt });
        });

        g.MapPost("/{id}/confirm-payment", async (
            Guid id,
            ProductStoreContext db,
            EmailNotificationQueue emailQueue,
            CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();
            var error = OrderWorkflow.ValidatePayment(command.PaymentStatus, "Deposit paid");
            if (error is not null) return Results.BadRequest(error);

            command.PaymentStatus = "Deposit paid";
            command.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await NotifyPayment(emailQueue, db, command, "Deposit confirmed", cancellationToken);
            return Results.Ok(new { command.Id, command.Status, command.PaymentStatus });
        });

        g.MapPost("/{id}/mark-paid-in-full", async (
            Guid id,
            ProductStoreContext db,
            EmailNotificationQueue emailQueue,
            CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();
            var error = OrderWorkflow.ValidatePayment(command.PaymentStatus, "Paid in full");
            if (error is not null) return Results.BadRequest(error);

            command.PaymentStatus = "Paid in full";
            command.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await NotifyPayment(emailQueue, db, command, "Payment complete", cancellationToken);
            return Results.Ok(new { command.Id, command.Status, command.PaymentStatus });
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

    private static async Task NotifyPayment(
        EmailNotificationQueue queue,
        ProductStoreContext db,
        Command command,
        string subject,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FindAsync([command.CustomerId], cancellationToken);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Email)) return;

        queue.TryEnqueue(new EmailNotification(
            customer.Email,
            customer.FullName,
            $"{subject} - {command.ReferenceNumber}",
            $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:auto;color:#2a1f2d;line-height:1.5">
              <h2 style="color:#7d4e7e">{WebUtility.HtmlEncode(subject)}</h2>
              <p>Hello {WebUtility.HtmlEncode(customer.FullName)},</p>
              <p>Your payment for order <strong>{WebUtility.HtmlEncode(command.ReferenceNumber)}</strong> is now recorded as <strong>{WebUtility.HtmlEncode(command.PaymentStatus)}</strong>.</p>
              <p>Order status: <strong>{WebUtility.HtmlEncode(command.Status)}</strong>.</p>
              <p>Thank you for shopping with Beauty &amp; Shine.</p>
            </div>
            """));
    }

    private static string BuildCustomerStatusEmail(Command command, string customerName)
    {
        var tracking = string.IsNullOrWhiteSpace(command.TrackingNumber)
            ? ""
            : $"<p>Tracking number: <strong>{WebUtility.HtmlEncode(command.TrackingNumber)}</strong></p>";
        var note = string.IsNullOrWhiteSpace(command.StatusNote)
            ? ""
            : $"<p>{WebUtility.HtmlEncode(command.StatusNote)}</p>";

        return $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:auto;color:#2a1f2d;line-height:1.5">
          <h2 style="color:#7d4e7e">Order update: {WebUtility.HtmlEncode(command.Status)}</h2>
          <p>Hello {WebUtility.HtmlEncode(customerName)},</p>
          <p>Your order <strong>{WebUtility.HtmlEncode(command.ReferenceNumber)}</strong> is now <strong>{WebUtility.HtmlEncode(command.Status)}</strong>.</p>
          {note}
          {tracking}
          <p>Thank you for shopping with Beauty &amp; Shine.</p>
        </div>
        """;
    }
}
