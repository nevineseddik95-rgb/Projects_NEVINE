namespace ProductStore.Api.Models;

// one line of an order: a product, how many, and the price when it was ordered
public class CommandItem
{
    public Guid Id { get; set; }
    public Guid CommandId { get; set; }
    public Guid ProductId { get; set; }
    public string Model { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
