namespace ProductStore.Api.Services;

public static class OrderWorkflow
{
    public static readonly string[] Statuses = ["Pending", "Processing", "Shipped", "Delivered", "Cancelled"];
    public static readonly string[] PaymentStatuses = ["Unpaid", "Payment submitted", "Deposit paid", "Paid in full"];

    public static string? ValidateStatus(string currentStatus, string nextStatus, string paymentStatus)
    {
        if (!Statuses.Contains(nextStatus))
            return "Choose a valid order status.";
        if (!Statuses.Contains(currentStatus))
            return "This order has an unsupported status and must be corrected by an administrator.";
        if (currentStatus == nextStatus)
            return "The order already has this status.";

        var allowed = currentStatus switch
        {
            "Pending" => nextStatus is "Processing" or "Cancelled",
            "Processing" => nextStatus is "Shipped" or "Cancelled",
            "Shipped" => nextStatus is "Delivered",
            _ => false
        };
        if (!allowed)
            return $"An order cannot move from {currentStatus} to {nextStatus}.";

        var depositConfirmed = paymentStatus is "Deposit paid" or "Paid in full";
        if (nextStatus is "Processing" or "Shipped" && !depositConfirmed)
            return "Confirm the deposit before processing or shipping the order.";
        if (nextStatus == "Delivered" && paymentStatus != "Paid in full")
            return "The full balance must be paid before an order can be delivered.";
        return null;
    }

    public static string? ValidatePayment(string currentPayment, string nextPayment)
    {
        if (!PaymentStatuses.Contains(nextPayment))
            return "Choose a valid payment status.";
        if (currentPayment == nextPayment)
            return "The payment already has this status.";

        var allowed = currentPayment switch
        {
            "Unpaid" or "Payment submitted" => nextPayment == "Deposit paid",
            "Deposit paid" => nextPayment == "Paid in full",
            _ => false
        };
        return allowed ? null : $"A payment cannot move from {currentPayment} to {nextPayment}.";
    }
}
