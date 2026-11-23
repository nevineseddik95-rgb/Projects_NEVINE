using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using ProductStore.Api.Models;

namespace ProductStore.Api.Seed;

public static class LookupSeeder
{
    // change these lists to whatever you sell
    private static readonly string[] Genres =
    {
        "Bracelet", "Necklace", "Keychain"
    };

    private static readonly string[] Colors =
    {
        "Silver", "Gold", "Rose gold", "Black", "White",
        "Pink", "Purple", "Blue", "Green", "Red"
    };

    private static readonly string[] Sizes =
    {
        "Thin", "Regular", "Bold",
        "Small", "Medium", "Large", "One size"
    };

    /// Adds the starting lists. A list that already has rows is never touched.
    public static async Task SeedLookupsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();

        if (!await db.Genres.AnyAsync())
            db.Genres.AddRange(Genres.Select(n => new Genre { Name = n }));

        if (!await db.Colors.AnyAsync())
            db.Colors.AddRange(Colors.Select(n => new Color { Name = n }));

        if (!await db.Sizes.AnyAsync())
            db.Sizes.AddRange(Sizes.Select(n => new Size { Name = n }));

        await db.SaveChangesAsync();
    }
}