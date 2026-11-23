using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Models;
using ProductStore.Api.Models;
using ProductStore.Api.Services;
using System.Net.Mail;

namespace ProductStore.Api.Endpoints;

public class CheckoutItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CheckoutRequest
{
    public string FullName { get; set; } = "";
    public string? Email { get; set; }                 // optional
    public string? Phone { get; set; }                 // required: for the text message
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
        // public: customers order without logging in
        app.MapPost("/checkout", async (CheckoutRequest req, ProductStoreContext db, IConfiguration config, IEmailSender email, ISmsSender sms) =>
        {
            var name = (req.FullName ?? "").Trim();
            var phone = PhoneNumber.Normalize(req.Phone);
            var emailAddress = (req.Email ?? "").Trim().ToLowerInvariant();

            if (name.Length == 0 || string.IsNullOrWhiteSpace(req.DeliveryAddress))
                return Results.BadRequest("Name and a delivery address are required.");
            if (phone is null)
                return Results.BadRequest("Enter a valid phone number, for example +269 321 2345 or 321 2345.");
            if (emailAddress.Length > 0 && !MailAddress.TryCreate(emailAddress, out _))
                return Results.BadRequest("That email address does not look right. You can leave it empty.");
            if (!Methods.Contains(req.PaymentMethod))
                return Results.BadRequest("Choose how you will pay the deposit.");
            if (req.Items.Count == 0 || req.Items.Count > MaxLines)
                return Results.BadRequest("Your cart is empty.");
            if (req.Items.Any(i => i.Quantity < 1 || i.Quantity > MaxQuantity))
                return Results.BadRequest($"Each quantity must be between 1 and {MaxQuantity}.");

            // the prices come from the database, never from the browser
            var wanted = req.Items
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = Math.Min(MaxQuantity, g.Sum(i => i.Quantity)) })
                .ToList();
            var ids = wanted.Select(w => w.ProductId).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
            if (products.Count != ids.Count)
                return Results.BadRequest("One of the products in your cart is no longer available.");

            var lines = wanted
                .Select(w => (Item: products.First(p => p.Id == w.ProductId), Qty: w.Quantity))
                .ToList();
            var total = lines.Sum(l => l.Item.Price * l.Qty);

            var minDeposit = Math.Ceiling(total * 0.40m);       // 40%, whole francs, rounded up
            var deposit = Math.Round(req.DepositAmount, 0, MidpointRounding.AwayFromZero);
            if (deposit < minDeposit)
                return Results.BadRequest($"The deposit must be at least {minDeposit:0} KMF, which is 40% of the total.");
            if (deposit > total)
                return Results.BadRequest("The deposit cannot be more than the total.");

            // find the customer by email when one is given, otherwise by phone
            var customer = emailAddress.Length > 0
                ? await db.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == emailAddress)
                : null;
            customer ??= await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);

            if (customer is null)
            {
                customer = new Customer { FullName = name, Email = emailAddress, Phone = phone, Address = req.DeliveryAddress.Trim() };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }
            else if (emailAddress.Length > 0 && string.IsNullOrEmpty(customer.Email))
            {
                customer.Email = emailAddress;      // keep an email that is given later
                await db.SaveChangesAsync();
            }

            var first = lines[0].Item;
            var summary = string.Join(", ", lines.Select(l => l.Qty > 1 ? $"{l.Item.Model} x{l.Qty}" : l.Item.Model));

            var order = new Command
            {
                ReferenceNumber = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
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
            await db.SaveChangesAsync();

            db.CommandItems.AddRange(lines.Select(l => new CommandItem
            {
                CommandId = order.Id, ProductId = l.Item.Id, Model = l.Item.Model,
                UnitPrice = l.Item.Price, Quantity = l.Qty
            }));
            await db.SaveChangesAsync();

            var instructions = config[$"Payment:Instructions:{req.PaymentMethod}"] ?? "";
            var emailLines = lines.Select(l => (l.Item.Model, l.Qty, l.Item.Price)).ToList();

            // a failed email or text message never cancels the order
            var emailSent = emailAddress.Length > 0 && await email.SendAsync(
                emailAddress, name,
                $"Your order {order.ReferenceNumber} - Charm Store",
                BuildConfirmationEmail(order, name, instructions, emailLines));

            // plain ASCII keeps it in a single text message
            var smsSent = await sms.SendAsync(phone,
                $"Charm Store: order {order.ReferenceNumber} received. Pay the {Money(deposit)} deposit with {req.PaymentMethod}. Total {Money(total)}. Keep this reference.");

            await NotifyAdminAsync(email, config,
                $"New order {order.ReferenceNumber} - {summary}",
                BuildAdminNewOrderEmail(order, name, emailAddress.Length > 0 ? emailAddress : "(not given)", phone));

            return Results.Ok(new
            {
                reference = order.ReferenceNumber,
                items = lines.Select(l => new { model = l.Item.Model, quantity = l.Qty, lineTotal = l.Item.Price * l.Qty }),
                total,
                deposit,
                balance = total - deposit,
                method = req.PaymentMethod,
                instructions,
                emailSent,
                smsSent,
                phone
            });
        }).RequireRateLimiting("checkout");

        // the customer sends the transaction ID after paying
        app.MapPost("/checkout/{reference}/payment", async (string reference, PaymentNotice notice, ProductStoreContext db, IConfiguration config, IEmailSender email) =>
        {
            if (string.IsNullOrWhiteSpace(notice.TransactionId))
                return Results.BadRequest("Enter the transaction ID from your payment message.");

            var order = await db.Commands.FirstOrDefaultAsync(c => c.ReferenceNumber == reference);
            var customer = order is null ? null : await db.Customers.FindAsync(order.CustomerId);
            var emailAddress = (notice.Email ?? "").Trim().ToLowerInvariant();
            var phone = PhoneNumber.Normalize(notice.Phone);

            // the customer proves it is theirs with the email or the phone used for the order
            var matches = customer is not null &&
                ((emailAddress.Length > 0 && customer.Email.ToLower() == emailAddress) ||
                 (phone is not null && customer.Phone == phone));

            // same answer whether the reference or the contact is wrong, so references cannot be guessed
            if (order is null || customer is null || !matches)
                return Results.NotFound();
            if (order.PaymentStatus != "Unpaid")
                return Results.BadRequest("This payment is already being checked or confirmed.");

            order.PaymentReference = notice.TransactionId.Trim();
            order.PaymentStatus = "Payment submitted";
            await db.SaveChangesAsync();

            await NotifyAdminAsync(email, config,
                $"Payment submitted for {order.ReferenceNumber}",
                $"<p>{WebUtility.HtmlEncode(customer.FullName)} says they paid the deposit for order <strong>{WebUtility.HtmlEncode(order.ReferenceNumber)}</strong> ({Money(order.DepositAmount)}).</p>" +
                $"<p>Transaction ID: <strong>{WebUtility.HtmlEncode(order.PaymentReference)}</strong></p>" +
                "<p>Check it in your MVola statement, then press <em>Confirm deposit</em> on the admin Orders page.</p>");

            return Results.Ok();
        }).RequireRateLimiting("checkout");
    }
    private static string Money(decimal v) =>
        v.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ') + " KMF";

    private static async Task NotifyAdminAsync(IEmailSender email, IConfiguration config, string subject, string htmlBody)
    {
        var admin = config["Notifications:AdminEmail"];
        if (string.IsNullOrWhiteSpace(admin)) return;
        await email.SendAsync(admin, "Beauty & Shine admin", subject, htmlBody);
    }

    // every value typed by the customer is HTML-encoded before it goes into an email
    private static string BuildConfirmationEmail(Command o, string name, string instructions,
        List<(string Model, int Qty, decimal Unit)> lines)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var how = string.IsNullOrWhiteSpace(instructions)
            ? "We will contact you with the payment details."
            : E(instructions);

        var rows = string.Join("", lines.Select(l =>
            $"<tr><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec\">{E(l.Model)} x {l.Qty}</td><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{Money(l.Unit * l.Qty)}</td></tr>"));

        return $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:auto;color:#2a1f2d;line-height:1.5">
          <h2 style="color:#7d4e7e;margin-bottom:4px">Thank you for your order, {E(name)}</h2>
          <p>We have received your order. Your reference number is:</p>
          <p style="font-size:22px;font-weight:bold;letter-spacing:1px;margin:8px 0 16px">{E(o.ReferenceNumber)}</p>

          <table style="width:100%;border-collapse:collapse">
            {rows}
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec"><strong>Total</strong></td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right"><strong>{Money(o.Total)}</strong></td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec"><strong>Deposit to pay now</strong></td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right"><strong>{Money(o.DepositAmount)}</strong></td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Balance later</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right">{Money(o.Total - o.DepositAmount)}</td></tr>
            <tr><td style="padding:6px 0">Delivery address</td><td style="padding:6px 0;text-align:right">{E(o.DeliveryAddress)}</td></tr>
          </table>

          <h3 style="color:#7d4e7e;margin-top:24px">How to pay your deposit with {E(o.PaymentMethod)}</h3>
          <p>{how}</p>
          <p>After you pay, keep your payment confirmation message and enter its transaction ID on the confirmation page of our website. We confirm your order when we receive the deposit.</p>

          <p style="color:#75677a;font-size:13px;margin-top:24px">Please keep this email. You will need the reference number if you contact us.</p>
        </div>
        """;
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
            {Row("Payment method", E(o.PaymentMethod))}
            {Row("Delivery address", E(o.DeliveryAddress))}
          </table>
          <p>The order is waiting for the deposit. Open the Orders page in your admin to follow it up.</p>
        </div>
        """;
    }
}