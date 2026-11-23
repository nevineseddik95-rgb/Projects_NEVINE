public record ProductDtoDetails(Guid Id, string Model, Guid GenreId, Guid ColorId, Guid SizeId,
    decimal Price, DateOnly ReleaseDate, string? ImageUrl);