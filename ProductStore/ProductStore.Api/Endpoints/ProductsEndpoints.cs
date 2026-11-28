using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using productStore.Api.Data;
using ProductStore.Api.Dtos;
using ProductStore.Api.Models;

namespace ProductStore.Api.Endpoints;

public static class ProductsEndpoints
{
    const string GetProductEndpointName = "GetProduct";

    public static void MapProductsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products");

        //GET/Products
       group.MapGet("/", async (ProductStoreContext dbContext) =>
{
    var items = await dbContext.Products
        .AsNoTracking()
        .Select(product => new ProductSummaryDto(
            product.Id,
            product.Model,
            product.genre!.Name,
            product.Size!.Name,
            product.Color!.Name,
            product.Price,
            product.ReleaseDate,
            product.ImageUrl,
            product.GenreId,
            product.ColorId,
            product.SizeId))
        .ToListAsync();
    return Results.Ok(items);
});



        //Get Product by ID 

        group.MapGet("/{id}", async (Guid id, ProductStoreContext dbContext) =>
        {
            var Product = await dbContext.Products
                .Include(product => product.genre)
                .Include(product => product.Color)
                .Include(product => product.Size)
                .FirstOrDefaultAsync(product => product.Id == id);

            return Product is not null ? Results.Ok(
                new
                {
                    Id = Product.Id,
                    Model = Product.Model,
                    GenreId = Product.GenreId,
                    SizeId = Product.SizeId,
                    ColorId = Product.ColorId,
                    Price = Product.Price,
                    ReleaseDate = Product.ReleaseDate,
                    ImageUrl = Product.ImageUrl,
                    Genre = Product.genre?.Name,
                    Color = Product.Color?.Name,
                    Size = Product.Size?.Name
                })
                : Results.NotFound();
        }).WithName(GetProductEndpointName);

        //POST/Product
        group.MapPost("/", async (CreateProductDto newProduct, ProductStoreContext dbContext) =>
        {
            Product Product = new()
            {
                Model = newProduct.Model,
                GenreId = newProduct.GenreId,
                SizeId = newProduct.SizeId,
                ColorId = newProduct.ColorId,
                Price = newProduct.Price,
                ReleaseDate = newProduct.ReleaseDate,ImageUrl = newProduct.ImageUrl 
            };
            dbContext.Products.Add(Product);
            await dbContext.SaveChangesAsync();

            ProductDtoDetails productDto = new(
                Product.Id,
                Product.Model,
                Product.GenreId,
                Product.ColorId,
                Product.SizeId,
                Product.Price,
                Product.ReleaseDate,
                Product.ImageUrl
            );


            return Results.CreatedAtRoute(GetProductEndpointName, new { id = productDto.Id }, productDto);
        });

        //PUT/Products/1
        group.MapPut("/{id}", async (Guid id, UpdateProductDto updatedProduct, ProductStoreContext dbContext) =>
        {
            var ExistingProduct = await dbContext.Products.FindAsync(id);
            if (ExistingProduct is null)
            {
                return Results.NotFound();
            }
            ExistingProduct.Model = updatedProduct.Model;
            ExistingProduct.GenreId = updatedProduct.GenreId;
            ExistingProduct.SizeId = updatedProduct.SizeId;
            ExistingProduct.ColorId = updatedProduct.ColorId;
            ExistingProduct.Price = updatedProduct.Price;
            ExistingProduct.ReleaseDate = updatedProduct.ReleaseDate;
            ExistingProduct.ImageUrl=updatedProduct.ImageUrl;
            await dbContext.SaveChangesAsync();
            return Results.NoContent();
        });

        //DELETE/Products/1
        group.MapDelete("/{id}", async (Guid id, ProductStoreContext dbContext) =>
        {
            await dbContext.Products
                                .Where(product => product.Id == id)
                                .ExecuteDeleteAsync();
            return Results.NoContent();


        });
    }
}

