namespace productStore.Api.Models;

public class Command
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = "";   // made by the API
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public string Model { get; set; } = "";             // copied from the product
    public Guid GenreId { get; set; }
    public Guid ColorId { get; set; }
    public Guid? SizeId { get; set; }
    public string DeliveryAddress { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "Pending";    // Pending, Processing, Shipped, Delivered, Cancelled
    public decimal Total { get; set; }
    public string? Note { get; set; }
    public string? ImageUrl { get; set; }
    public decimal DepositAmount { get; set; }
public string PaymentStatus { get; set; } = "Unpaid";   // Unpaid, Deposit paid, Paid in full
public string? PaymentReference { get; set; }           // transaction ID, filled in by you
public string? PaymentMethod { get; set; }   
}
