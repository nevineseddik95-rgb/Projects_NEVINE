using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Models;
using ProductStore.Api.Models;
using ProductStore.Api.Services;

namespace ProductStore.Api.Endpoints;

public class CheckoutItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CheckoutRequest
{
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool SmsUpdates { get; set; } = true;
    public string DeliveryAddress { get; set; } = "";
    public decimal DepositAmount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public List<CheckoutItem> Items { get; set; } = new();
}

public class PaymentNotice
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string TransactionId { get; set; } = "";
}

public static class CheckoutEndpoints
{
    private static readonly string[] Methods = { "MVola" };
    private const int MaxQuantity = 10;
    private const int MaxLines = 20;

    public static void MapCheckoutEndpoints(this WebApplication app)
    {
        app.MapPost("/checkout", async (
            CheckoutRequest req,
            ProductStoreContext db,
            IConfiguration config,
            EmailNotificationQueue emailQueue,
            OrderNotifications notifications,
            AdminChangeNotifier realtime,
            CancellationToken cancellationToken) =>
        {
            var name = (req.FullName ?? "").Trim();
            var phone = PhoneNumber.Normalize(req.Phone);
            if (!EmailAddress.TryNormalize(req.Email, out var emailAddress))
                return Results.BadRequest("That email address does not look right. You can leave it empty.");

            if (name.Length == 0 || string.IsNullOrWhiteSpace(req.DeliveryAddress))
                return Results.BadRequest("Name and a delivery address are required.");
            if (phone is null)
                return Results.BadRequest("Enter a valid phone number, for example +269 321 2345 or 321 2345.");
            if (!Methods.Contains(req.PaymentMethod))
                return Results.BadRequest("Choose how you will pay the deposit.");
            if (req.Items.Count == 0 || req.Items.Count > MaxLines)
                return Results.BadRequest("Your cart is empty.");
            if (req.Items.Any(i => i.Quantity < 1 || i.Quantity > MaxQuantity))
                return Results.BadRequest($"Each quantity must be between 1 and {MaxQuantity}.");

            var wanted = req.Items
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = Math.Min(MaxQuantity, g.Sum(i => i.Quantity)) })
                .ToList();
            var ids = wanted.Select(w => w.ProductId).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
            if (products.Count != ids.Count)
                return Results.BadRequest("One of the products in your cart is no longer available.");

            var lines = wanted
                .Select(w => (Item: products.First(p => p.Id == w.ProductId), Qty: w.Quantity))
                .ToList();
            var total = lines.Sum(l => l.Item.Price * l.Qty);
            var minDeposit = Math.Ceiling(total * 0.40m);
            var deposit = Math.Round(req.DepositAmount, 0, MidpointRounding.AwayFromZero);
            if (deposit < minDeposit)
                return Results.BadRequest($"The deposit must be at least {minDeposit:0} KMF, which is 40% of the total.");
            if (deposit > total)
                return Results.BadRequest("The deposit cannot be more than the total.");

            var normalizedName = name.ToLowerInvariant();
            var customer = emailAddress.Length > 0
                ? await db.Customers.FirstOrDefaultAsync(c =>
                    c.Email.ToLower() == emailAddress && c.FullName.ToLower() == normalizedName, cancellationToken)
                : null;
            customer ??= await db.Customers.FirstOrDefaultAsync(c =>
                c.Phone == phone && c.FullName.ToLower() == normalizedName, cancellationToken);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (customer is null)
            {
                customer = new Customer
                {
                    FullName = name,
                    Email = emailAddress,
                    Phone = phone,
                    SmsUpdates = req.SmsUpdates,
                    Address = req.DeliveryAddress.Trim()
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                if (emailAddress.Length > 0 && !string.Equals(customer.Email, emailAddress, StringComparison.OrdinalIgnoreCase))
                    customer.Email = emailAddress;
                customer.FullName = name;
                customer.Phone = phone;
                customer.Address = req.DeliveryAddress.Trim();
                customer.SmsUpdates = req.SmsUpdates;
                await db.SaveChangesAsync(cancellationToken);
            }

            var first = lines[0].Item;
            var summary = string.Join(", ", lines.Select(l => l.Qty > 1 ? $"{l.Item.Model} x{l.Qty}" : l.Item.Model));
            var order = new Command
            {
                ReferenceNumber = OrderTracking.NewReferenceNumber(),
                CustomerId = customer.Id,
                ProductId = first.Id,
                Model = summary,
                ImageUrl = first.ImageUrl,
                GenreId = first.GenreId,
                ColorId = first.ColorId,
                SizeId = first.SizeId,
                DeliveryAddress = req.DeliveryAddress.Trim(),
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = "Pending",
                Total = total,
                DepositAmount = deposit,
                PaymentStatus = "Unpaid",
                PaymentMethod = req.PaymentMethod
            };
            db.Commands.Add(order);
            await db.SaveChangesAsync(cancellationToken);

            db.CommandItems.AddRange(lines.Select(l => new CommandItem
            {
                CommandId = order.Id,
                ProductId = l.Item.Id,
                Model = l.Item.Model,
                UnitPrice = l.Item.Price,
                Quantity = l.Qty
            }));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var instructions = config[$"Payment:Instructions:{req.PaymentMethod}"] ?? "";
            var notificationResult = notifications.QueueOrderCreated(order, customer, instructions);

            NotifyAdmin(emailQueue, config,
                $"New order {order.ReferenceNumber} - {summary}",
                BuildAdminNewOrderEmail(order, name, emailAddress.Length > 0 ? emailAddress : "(not given)", phone));
            await realtime.NotifyAsync(["commands", "customers", "commissions"], cancellationToken);

            return Results.Ok(new
            {
                reference = order.ReferenceNumber,
                items = lines.Select(l => new { model = l.Item.Model, quantity = l.Qty, lineTotal = l.Item.Price * l.Qty }),
                total,
                deposit,
                balance = total - deposit,
                method = req.PaymentMethod,
                instructions,
                emailQueued = notificationResult.EmailQueued,
                smsQueued = notificationResult.SmsQueued,
                smsRequested = customer.SmsUpdates,
                phone
            });
        }).RequireRateLimiting("checkout");

        app.MapPost("/checkout/{reference}/payment", async (
            string reference,
            PaymentNotice notice,
            ProductStoreContext db,
            IConfiguration config,
            EmailNotificationQueue emailQueue,
            OrderNotifications notifications,
            AdminChangeNotifier realtime,
            CancellationToken cancellationToken) =>
        {
            var transactionId = (notice.TransactionId ?? "").Trim();
            if (transactionId.Length is < 3 or > 100)
                return Results.BadRequest("Enter a transaction ID between 3 and 100 characters.");

            var order = await db.Commands.FirstOrDefaultAsync(c => c.ReferenceNumber == reference, cancellationToken);
            var customer = order is null ? null : await db.Customers.FindAsync([order.CustomerId], cancellationToken);
            if (!EmailAddress.TryNormalize(notice.Email, out var emailAddress))
                return Results.BadRequest("That email address does not look right.");
            var phone = PhoneNumber.Normalize(notice.Phone);
            var matches = customer is not null &&
                ((emailAddress.Length > 0 && customer.Email.ToLower() == emailAddress) ||
                 (phone is not null && customer.Phone == phone));

            if (order is null || customer is null || !matches)
                return Results.NotFound();

            if (order.PaymentStatus == "Payment submitted" &&
                string.Equals(order.PaymentReference, transactionId, StringComparison.OrdinalIgnoreCase))
                return Results.Ok(new { received = true, duplicate = true, emailQueued = false, smsQueued = false });
            if (order.PaymentStatus != "Unpaid")
                return Results.BadRequest("This payment is already being checked or confirmed.");
            if (await db.Commands.AnyAsync(c => c.Id != order.Id && c.PaymentReference == transactionId, cancellationToken))
                return Results.Conflict("This transaction ID is already linked to another order. Check it and try again.");

            order.PaymentReference = transactionId;
            order.PaymentStatus = "Payment submitted";
            order.UpdatedAt = DateTime.UtcNow;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Results.Conflict("This transaction ID is already linked to another order. Check it and try again.");
            }

            var notificationResult = notifications.QueuePaymentSubmitted(order, customer);
            NotifyAdmin(emailQueue, config,
                $"Payment submitted for {order.ReferenceNumber}",
                $"<p>{WebUtility.HtmlEncode(customer.FullName)} says they paid the deposit for order <strong>{WebUtility.HtmlEncode(order.ReferenceNumber)}</strong> ({Money(order.DepositAmount)}).</p>" +
                $"<p>Transaction ID: <strong>{WebUtility.HtmlEncode(order.PaymentReference)}</strong></p>" +
                "<p>Check it in your MVola statement, then press <em>Confirm deposit</em> on the admin Orders page.</p>");
            await realtime.NotifyAsync(["commands"], cancellationToken);

            return Results.Ok(new
            {
                received = true,
                duplicate = false,
                emailQueued = notificationResult.EmailQueued,
                smsQueued = notificationResult.SmsQueued
            });
        }).RequireRateLimiting("checkout");
    }

    private static string Money(decimal v) =>
        v.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ') + " KMF";

    private static void NotifyAdmin(EmailNotificationQueue queue, IConfiguration config, string subject, string htmlBody)
    {
        var admin = config["Notifications:AdminEmail"];
        if (EmailAddress.TryNormalize(admin, out var adminEmail) && adminEmail.Length > 0)
            queue.TryEnqueue(new EmailNotification(adminEmail, "Beauty & Shine admin", subject, htmlBody));
    }

    private static string BuildAdminNewOrderEmail(Command o, string name, string emailAddress, string? phone)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        string Row(string k, string v) =>
            $"<tr><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec\">{k}</td><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{v}</td></tr>";

        return $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:auto;color:#2a1f2d;line-height:1.5">
          <h2 style="color:#7d4e7e">New order {E(o.ReferenceNumber)}</h2>
          <table style="width:100%;border-collapse:collapse">
            {Row("Customer", E(name))}
            {Row("Phone", E(phone))}
            {Row("Email", E(emailAddress))}
            {Row("Items", E(o.Model))}
            {Row("Total", Money(o.Total))}
            {Row("Deposit due", Money(o.DepositAmount))}
            {Row("Status", E(o.Status))}
            {Row("Payment", E(o.PaymentStatus))}
            {Row("Payment method", E(o.PaymentMethod))}
            {Row("Delivery address", E(o.DeliveryAddress))}
          </table>
          <p>The order is waiting for the deposit. Open the Orders page in your admin to follow it up.</p>
        </div>
        """;
    }
}
