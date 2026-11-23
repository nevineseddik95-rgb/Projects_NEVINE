namespace ProductStore.Api.Dtos;

public record ProductSummaryDto(Guid Id, string Model, string Genre, string Size, string Color,
    decimal Price, DateOnly ReleaseDate, string? ImageUrl, Guid GenreId, Guid ColorId, Guid SizeId);


