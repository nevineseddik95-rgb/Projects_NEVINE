namespace ProductStore.Api.Models;

public class Product
{
    public Guid Id { get; set; }
    public string Model { get; set; } = "";

    public Guid GenreId { get; set; }
    public Genre? genre { get; set; }

    public Guid ColorId { get; set; }
    public Color? Color { get; set; }

    public Guid SizeId { get; set; }
    public Size? Size { get; set; }

    public decimal Price { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public string? ImageUrl { get; set; }
}