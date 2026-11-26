namespace productStore.Api.Models;

public class Command
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public string Model { get; set; } = "";
    public Guid GenreId { get; set; }
    public Guid ColorId { get; set; }
    public Guid? SizeId { get; set; }
    public string DeliveryAddress { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "Pending";
    public string? StatusNote { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime StatusUpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
    public string? ImageUrl { get; set; }
    public decimal DepositAmount { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public string? PaymentReference { get; set; }
    public string? PaymentMethod { get; set; }
}
