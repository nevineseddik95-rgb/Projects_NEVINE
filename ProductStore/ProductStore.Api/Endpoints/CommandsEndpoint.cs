using System.Security.Claims;
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

public sealed record RecordCommissionPayment(decimal? Amount, string? Note = null);

public sealed record OrderResponse(
    Guid Id,
    string ReferenceNumber,
    Guid CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    Guid? CreatedByAdminId,
    string? CreatedByAdminName,
    decimal CommissionAmount,
    Guid ProductId,
    string Model,
    Guid GenreId,
    Guid ColorId,
    Guid? SizeId,
    string DeliveryAddress,
    DateOnly Date,
    string Status,
    string? StatusNote,
    string? TrackingNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime StatusUpdatedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    decimal Total,
    string? Note,
    string? ImageUrl,
    decimal DepositAmount,
    string PaymentStatus,
    string? PaymentReference,
    string? PaymentMethod);

public sealed record CommissionResponse(
    Guid OrderId,
    string ReferenceNumber,
    Guid AdminId,
    string AdminName,
    string? CustomerName,
    DateOnly OrderDate,
    DateTime CreatedAt,
    string Status,
    string PaymentStatus,
    decimal OrderTotal,
    decimal CommissionRate,
    decimal CommissionAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string PayoutStatus,
    DateTime? PaidAt,
    string? PaymentNote,
    bool Eligible);

public static class CommandsEndpoints
{
    private const decimal CommissionRate = 0.05m;

    public static void MapCommandsEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/commands").RequireAuthorization("Staff");

        g.MapGet("/", async (ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var orders = await QueryOrders(db).ToListAsync(cancellationToken);
            return Results.Ok(orders);
        });

        g.MapGet("/{id}", async (Guid id, ProductStoreContext db, CancellationToken cancellationToken) =>
            await QueryOrders(db, id).SingleOrDefaultAsync(cancellationToken) is { } order
                ? Results.Ok(order)
                : Results.NotFound());

        g.MapPost("/", async (
            SaveCommand dto,
            ProductStoreContext db,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var product = await db.Products.FindAsync([dto.ProductId], cancellationToken);
            if (product is null) return Results.BadRequest("Product not found.");
            if (!await db.Customers.AnyAsync(c => c.Id == dto.CustomerId, cancellationToken))
                return Results.BadRequest("Customer not found.");
            if (dto.Total < 0) return Results.BadRequest("The order total cannot be negative.");
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var creatorId))
                return Results.Unauthorized();
            var creator = await db.AdminUsers.FindAsync([creatorId], cancellationToken);
            if (creator is null) return Results.Unauthorized();

            var referenceError = await ValidatePaymentReferenceAsync(db, dto.PaymentReference, null, cancellationToken);
            if (referenceError is not null) return Results.BadRequest(referenceError);

            var command = new Command
            {
                ReferenceNumber = OrderTracking.NewReferenceNumber(),
                CreatedByAdminId = creator.Id,
                Model = product.Model,
                Status = "Pending",
                PaymentStatus = "Unpaid"
            };
            Fill(command, dto);
            command.CommissionAmount = creator.Role == "Owner" ? 0m : CommissionFor(command.Total);
            if (string.IsNullOrEmpty(command.ImageUrl)) command.ImageUrl = product.ImageUrl;

            db.Commands.Add(command);
            await db.SaveChangesAsync(cancellationToken);
            var response = await QueryOrders(db, command.Id).SingleAsync(cancellationToken);
            return Results.Created($"/commands/{command.Id}", response);
        });

        g.MapPut("/{id}", async (Guid id, SaveCommand dto, ProductStoreContext db, CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (command is null) return Results.NotFound();
            if (dto.Total < 0) return Results.BadRequest("The order total cannot be negative.");

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

            var earnsCommission = command.CreatedByAdminId is not null && await db.AdminUsers.AnyAsync(
                u => u.Id == command.CreatedByAdminId && u.Role != "Owner",
                cancellationToken);
            var nextCommission = earnsCommission ? CommissionFor(dto.Total) : 0m;
            if (command.CommissionPaidAmount > nextCommission)
                return Results.BadRequest("Reduce or reverse the recorded commission payout before lowering this order total.");

            Fill(command, dto);
            if (command.CreatedByAdminId is not null) command.CommissionAmount = nextCommission;
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
            if (note?.Length > 1000) return Results.BadRequest("The status note cannot be longer than 1,000 characters.");

            var tracking = command.TrackingNumber;
            if (nextStatus == "Shipped" && string.IsNullOrWhiteSpace(tracking))
            {
                do
                {
                    tracking = OrderTracking.NewTrackingNumber();
                } while (await db.Commands.AnyAsync(c => c.TrackingNumber == tracking, cancellationToken));
            }

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
                command.TrackingNumber,
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

        var commissions = app.MapGroup("/commissions").RequireAuthorization("Staff");
        commissions.MapGet("/", async (
            ClaimsPrincipal user,
            ProductStoreContext db,
            CancellationToken cancellationToken) =>
        {
            Guid? viewerAdminId = null;
            if (!user.IsInRole("Owner"))
            {
                if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
                    return Results.Unauthorized();
                viewerAdminId = adminId;
            }

            var rows = await QueryCommissions(db, viewerAdminId).ToListAsync(cancellationToken);
            return Results.Ok(rows);
        });

        commissions.MapPost("/{orderId}/payment", async (
            Guid orderId,
            RecordCommissionPayment dto,
            ProductStoreContext db,
            CancellationToken cancellationToken) =>
        {
            var command = await db.Commands.FirstOrDefaultAsync(c => c.Id == orderId, cancellationToken);
            if (command is null) return Results.NotFound();
            if (command.Status == "Cancelled")
                return Results.BadRequest("Cancelled orders cannot receive a commission payout.");
            if (command.CreatedByAdminId is null || !await db.AdminUsers.AnyAsync(
                u => u.Id == command.CreatedByAdminId && u.Role != "Owner", cancellationToken))
                return Results.BadRequest("This order is not eligible for a staff commission.");

            if (dto.Amount is null) return Results.BadRequest("A payout amount is required.");
            var amount = decimal.Round(dto.Amount.Value, 2, MidpointRounding.AwayFromZero);
            if (amount < 0 || amount > command.CommissionAmount)
                return Results.BadRequest("The payout must be between zero and the commission amount.");
            var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
            if (note?.Length > 1000)
                return Results.BadRequest("The payout note cannot be longer than 1,000 characters.");

            command.CommissionPaidAmount = amount;
            command.CommissionPaidAt = amount > 0 ? DateTime.UtcNow : null;
            command.CommissionPaymentNote = note;
            command.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var response = await QueryCommissions(db, orderId: orderId).SingleAsync(cancellationToken);
            return Results.Ok(response);
        }).RequireAuthorization("Owner");
    }

    private static IQueryable<CommissionResponse> QueryCommissions(
        ProductStoreContext db,
        Guid? viewerAdminId = null,
        Guid? orderId = null)
    {
        var query =
            from command in db.Commands.AsNoTracking()
            join admin in db.AdminUsers.AsNoTracking() on command.CreatedByAdminId equals admin.Id
            join customer in db.Customers.AsNoTracking() on command.CustomerId equals customer.Id into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            where command.CreatedByAdminId != null && admin.Role != "Owner"
                && (viewerAdminId == null || admin.Id == viewerAdminId)
                && (orderId == null || command.Id == orderId)
            orderby command.CreatedAt descending, command.Id descending
            select new CommissionResponse(
                command.Id,
                command.ReferenceNumber,
                admin.Id,
                admin.Username,
                customer.FullName,
                command.Date,
                command.CreatedAt,
                command.Status,
                command.PaymentStatus,
                command.Total,
                CommissionRate,
                command.Status == "Cancelled" ? 0m : command.CommissionAmount,
                command.CommissionPaidAmount,
                command.Status == "Cancelled" || command.CommissionPaidAmount >= command.CommissionAmount
                    ? 0m
                    : command.CommissionAmount - command.CommissionPaidAmount,
                command.Status == "Cancelled"
                    ? "Excluded"
                    : command.CommissionPaidAmount >= command.CommissionAmount
                        ? "Paid"
                        : command.CommissionPaidAmount > 0 ? "PartiallyPaid" : "Unpaid",
                command.CommissionPaidAt,
                command.CommissionPaymentNote,
                command.Status != "Cancelled");

        return query;
    }

    private static IQueryable<OrderResponse> QueryOrders(ProductStoreContext db, Guid? id = null)
    {
        var query =
            from command in db.Commands.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on command.CustomerId equals customer.Id into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join creator in db.AdminUsers.AsNoTracking() on command.CreatedByAdminId equals creator.Id into creatorGroup
            from creator in creatorGroup.DefaultIfEmpty()
            where id == null || command.Id == id
            orderby command.CreatedAt descending, command.Id descending
            select new OrderResponse(
                command.Id,
                command.ReferenceNumber,
                command.CustomerId,
                customer.FullName,
                customer.Email,
                customer.Phone,
                command.CreatedByAdminId,
                creator.Username,
                command.CommissionAmount,
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
                command.PaymentMethod);

        return query;
    }

    private static decimal CommissionFor(decimal total) =>
        Math.Round(total * CommissionRate, 2, MidpointRounding.AwayFromZero);

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
