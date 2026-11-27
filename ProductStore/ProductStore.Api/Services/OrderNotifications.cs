using System.Globalization;
using System.Net;
using productStore.Api.Models;

namespace ProductStore.Api.Services;

public sealed record NotificationResult(bool EmailQueued, bool SmsQueued);

public sealed class OrderNotifications(
    EmailNotificationQueue emailQueue,
    SmsNotificationQueue smsQueue,
    IConfiguration config)
{
    public NotificationResult QueueOrderCreated(
        Command order,
        Customer customer,
        string? paymentInstructions) =>
        Queue(
            order,
            customer,
            "Order received",
            "We have received your order. Pay the deposit to start processing.",
            string.IsNullOrWhiteSpace(paymentInstructions) ? null : $"<h3>How to pay</h3><p>{WebUtility.HtmlEncode(paymentInstructions)}</p>");

    public NotificationResult QueuePaymentSubmitted(Command order, Customer customer) =>
        Queue(
            order,
            customer,
            "Payment received for review",
            "We received your transaction ID and will confirm the payment after checking it.");

    public NotificationResult QueuePaymentConfirmed(
        Command order,
        Customer customer,
        string headline) =>
        Queue(
            order,
            customer,
            headline,
            headline == "Payment complete"
                ? "Your payment is complete. Thank you."
                : "Your deposit is confirmed. We can now process your order.");

    public NotificationResult QueueStatusChanged(
        Command order,
        Customer customer,
        string previousStatus) =>
        Queue(
            order,
            customer,
            $"Order update: {order.Status}",
            $"Your order status changed from {previousStatus} to {order.Status}.");

    private NotificationResult Queue(
        Command order,
        Customer customer,
        string headline,
        string introduction,
        string? extraHtml = null)
    {
        var emailQueued = !string.IsNullOrWhiteSpace(customer.Email)
            && emailQueue.TryEnqueue(new EmailNotification(
                customer.Email,
                customer.FullName,
                $"{headline} - {order.ReferenceNumber}",
                BuildEmail(order, customer, headline, introduction, extraHtml)));

        var normalizedPhone = PhoneNumber.Normalize(customer.Phone);
        var smsQueued = customer.SmsUpdates
            && SmsSettings.IsEnabled(config)
            && normalizedPhone is not null
            && smsQueue.TryEnqueue(new SmsNotification(
                normalizedPhone,
                BuildSms(order, headline)));

        return new NotificationResult(emailQueued, smsQueued);
    }

    private static string BuildEmail(
        Command order,
        Customer customer,
        string headline,
        string introduction,
        string? extraHtml)
    {
        string E(string? value) => WebUtility.HtmlEncode(value ?? "");
        var tracking = string.IsNullOrWhiteSpace(order.TrackingNumber)
            ? ""
            : $"<tr><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec\">Tracking number</td><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\"><strong>{E(order.TrackingNumber)}</strong></td></tr>";
        var note = string.IsNullOrWhiteSpace(order.StatusNote)
            ? ""
            : $"<p><strong>Latest update:</strong> {E(order.StatusNote)}</p>";
        var paymentReference = string.IsNullOrWhiteSpace(order.PaymentReference)
            ? ""
            : $"<tr><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec\">Transaction ID</td><td style=\"padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{E(order.PaymentReference)}</td></tr>";

        return $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:auto;color:#2a1f2d;line-height:1.5">
          <h2 style="color:#7d4e7e;margin-bottom:4px">{E(headline)}</h2>
          <p>Hello {E(customer.FullName)},</p>
          <p>{E(introduction)}</p>
          <p>Your order reference is <strong>{E(order.ReferenceNumber)}</strong>.</p>
          <table style="width:100%;border-collapse:collapse">
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Order status</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\"><strong>{E(order.Status)}</strong></td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Payment status</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\"><strong>{E(order.PaymentStatus)}</strong></td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Items</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{E(order.Model)}</td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Total</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{Money(order.Total)}</td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Deposit</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{Money(order.DepositAmount)}</td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Balance</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{Money(Balance(order))}</td></tr>
            <tr><td style="padding:6px 0;border-bottom:1px solid #ebe0ec">Payment method</td><td style="padding:6px 0;border-bottom:1px solid #ebe0ec;text-align:right\">{E(order.PaymentMethod)}</td></tr>
            {paymentReference}
            {tracking}
            <tr><td style="padding:6px 0">Delivery address</td><td style="padding:6px 0;text-align:right\">{E(order.DeliveryAddress)}</td></tr>
          </table>
          {note}
          {extraHtml}
          <p style="color:#75677a;font-size:13px;margin-top:24px">Keep this email and your reference number for follow-up. Beauty &amp; Shine.</p>
        </div>
        """;
    }

    private static string BuildSms(Command order, string headline)
    {
        var lines = new List<string>
        {
            "Beauty & Shine",
            headline,
            $"Ref: {order.ReferenceNumber}",
            $"Status: {order.Status} | Payment: {order.PaymentStatus}",
            $"Items: {Compact(order.Model, 120)}",
            $"Total: {Money(order.Total)} | Deposit: {Money(order.DepositAmount)} | Balance: {Money(Balance(order))}"
        };

        if (!string.IsNullOrWhiteSpace(order.TrackingNumber))
            lines.Add($"Tracking: {Compact(order.TrackingNumber, 100)}");
        if (!string.IsNullOrWhiteSpace(order.PaymentReference))
            lines.Add($"Transaction: {Compact(order.PaymentReference, 100)}");
        if (!string.IsNullOrWhiteSpace(order.StatusNote))
            lines.Add($"Update: {Compact(order.StatusNote, 120)}");

        lines.Add("Keep this reference for follow-up.");
        return string.Join("\n", lines);
    }

    private static decimal Balance(Command order) =>
        order.PaymentStatus == "Paid in full" ? 0 : Math.Max(0, order.Total - order.DepositAmount);

    private static string Money(decimal value) =>
        value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ') + " KMF";

    private static string Compact(string? value, int maxLength)
    {
        var text = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }
}
