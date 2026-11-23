using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Dtos;
using ProductStore.Api.Models;
namespace productStore.Api.Endpoints;

public static class ColorEndPoints
{
public static void MapColorsEndpPoints(this WebApplication application)
    {
        const string GetColorEndpointName = "GetColor";
        var group = application.MapGroup("/colors");

        //GEt/colors
        group.MapGet("/", async (ProductStoreContext dbContext) =>
        {
          var items=  await dbContext.Colors
            .Select(Color => new ColorDto(Color.Id, Color.Name))
            .AsNoTracking()
            .ToListAsync();
            return items;
        });

        //GET/color/id
        group.MapGet("/{id}", async (Guid id, ProductStoreContext dbContext) =>
        {
            var Color = await dbContext.Colors.FindAsync(id);
            return Color is not null ? Results.Ok(
                new
                {
                    Id = Color.Id,
                    Name = Color.Name
                }
            ) : Results.NotFound();
        }).WithName(GetColorEndpointName);

        //POST/color/
        group.MapPost("/", async (CreateColorDto newColor, ProductStoreContext dbContext) =>
        {
            Color Color = new()
            {
                Name = newColor.Name
            };
            dbContext.Colors.Add(Color);
            await dbContext.SaveChangesAsync();

            ColorDto ColorDto = new(
                Color.Id,
                Color.Name
            );


            return Results.CreatedAtRoute("getColor", new { Id = ColorDto.Id }, ColorDto);
        }).RequireAuthorization();

        //PUT/color/1
        group.MapPut("/{id}", async (Guid id, UpdateColorDto updatedColor, ProductStoreContext dbContext) =>
        {
            var ExistingColor = await dbContext.Colors.FindAsync(id);
            if (ExistingColor is null)
            {
                return Results.NotFound();
            }
            ExistingColor.Name = updatedColor.Name;
            await dbContext.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        //DELETE/color/1
        group.MapDelete("/{id}", async (Guid id, ProductStoreContext dbContext) =>
        {
            await dbContext.Colors
                                .Where(color => color.Id == id)
                                .ExecuteDeleteAsync();
            return Results.NoContent();


        }).RequireAuthorization();

    }
}
