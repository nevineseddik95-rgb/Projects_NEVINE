public record CreateProductDto(string Model, Guid GenreId, Guid ColorId, Guid SizeId,
    decimal Price, DateOnly ReleaseDate, string? ImageUrl);