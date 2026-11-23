using System.Reflection.Metadata.Ecma335;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using productStore.Api.Dtos;
using ProductStore.Api.Models;

namespace productStore.Api.Endpoints;

public static class GenresEndPoint
{
    public static void MapGenreEndpPoints(this WebApplication application)
    {
        const string GetGenreEndpointName = "GetGenre";
        var group = application.MapGroup("/genres");

        //GEt/genres
        group.MapGet("/", async (ProductStoreContext dbContext) =>
        {
           var items= await dbContext.Genres
            .Select(genre => new GenreDto(genre.Id,genre.Name))
            .AsNoTracking()
            .ToListAsync();
            return items;
        });

        //GET/genres/id
        group.MapGet("/{id}",async(Guid id,ProductStoreContext dbContext) =>
        {
            var genre= await dbContext.Genres.FindAsync(id);
            return genre is not null? Results.Ok(
                new
                {
                    Id=genre.Id,
                    Name=genre.Name
                }
            ):Results.NotFound();
        }).WithName(GetGenreEndpointName);

        //POST/genres/
        group.MapPost("/", async (CreateGenreDto newProduct, ProductStoreContext dbContext) =>
        {
            Genre Genre = new()
            {
                Name=newProduct.Name
            };
            dbContext.Genres.Add(Genre);
            await dbContext.SaveChangesAsync();

            GenreDto genreDto = new(
                Genre.Id,
                Genre.Name
            );


            return Results.CreatedAtRoute("getGenre", new { Id = genreDto.Id }, genreDto);
        }).RequireAuthorization();

        //PUT/genre/1
        group.MapPut("/{id}", async (Guid id, UpdateGenreDto updatedGenre, ProductStoreContext dbContext) =>
        {
            var ExistingGenre = await dbContext.Genres.FindAsync(id);
            if (ExistingGenre is null)
            {
                return Results.NotFound();
            }
            ExistingGenre.Name = updatedGenre.Name;
            await dbContext.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        //DELETE/genre/1
        group.MapDelete("/{id}", async (Guid id, ProductStoreContext dbContext) =>
        {
            await dbContext.Genres
                                .Where(genre => genre.Id == id)
                                .ExecuteDeleteAsync();
            return Results.NoContent();


        }).RequireAuthorization();

    }

}
