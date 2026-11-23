using System.Drawing;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Dtos;
using Size = ProductStore.Api.Models.Size;

namespace productStore.Api.Endpoints;

public static class SizesEndpoints
{
    public static void MapSizesEndpPoints(this WebApplication application)
    {
        const string GetSizeEndpointName = "GetSize";
        var group = application.MapGroup("/sizes");

        //GEt/sizes
        group.MapGet("/", async (ProductStoreContext dbContext) =>
        {
            var items = await dbContext.Sizes
            .Select(size => new SizeDto(size.Id, size.Name))
            .AsNoTracking()
            .ToListAsync();

            return items;
        });

        //GET/size/id
        group.MapGet("/{id}", async (Guid id, ProductStoreContext dbContext) =>
        {
            var size = await dbContext.Sizes.FindAsync(id);
            return size is not null ? Results.Ok(
                new
                {
                    Id = size.Id,
                    Name = size.Name
                }
            ) : Results.NotFound();
        }).WithName(GetSizeEndpointName);

        //POST/sizes/
        group.MapPost("/", async (CreateSizeDto newSize, ProductStoreContext dbContext) =>
        {
            Size size = new()
            {
                Name = newSize.Name
            };
            dbContext.Sizes.Add(size);
            await dbContext.SaveChangesAsync();

            SizeDto sizeDto = new(
                size.Id,
                size.Name
            );


            return Results.CreatedAtRoute("getSize", new { Id = sizeDto.Id }, sizeDto);
        }).RequireAuthorization();

        //PUT/size/1
        group.MapPut("/{id}", async (Guid id, UpdateSizeDto updatedSize, ProductStoreContext dbContext) =>
        {
            var ExistingSize = await dbContext.Sizes.FindAsync(id);
            if (ExistingSize is null)
            {
                return Results.NotFound();
            }
            ExistingSize.Name = updatedSize.Name;
            await dbContext.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        //DELETE/size/1
        group.MapDelete("/{id}", async (Guid id, ProductStoreContext dbContext) =>
        {
            await dbContext.Sizes
                                .Where(size => size.Id == id)
                                .ExecuteDeleteAsync();
            return Results.NoContent();


        }).RequireAuthorization();

    }
}
