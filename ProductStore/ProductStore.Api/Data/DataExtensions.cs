using Microsoft.EntityFrameworkCore;
using ProductStore.Api.Models;

namespace productStore.Api.Data;

public static class DataExtensions
{

    public static void MigrateDb(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider
                        .GetRequiredService<ProductStoreContext>();
        dbContext.Database.Migrate();
    }

    public static void AddProductStoreDb(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("ProductStore");
        builder.Services.AddSqlite<ProductStoreContext>(connectionString
     , optionsAction: options => options.UseSeeding((context, _) =>
     {
         if (!context.Set<Genre>().Any())
         {
             //data seeding
             context.Set<Genre>().AddRange(
                 new Genre { Name = "bracelet" },
                 new Genre { Name = "Necklace" },
                 new Genre { Name = "Keychain" },
                 new Genre { Name = "Ring" }
             );
            context.SaveChanges();
         }

         if (!context.Set<Size>().Any())
         {
             //data seeding
             context.Set<Size>().AddRange(
                 new Size { Name = "Bold" },
                 new Size { Name = "Thin" }
             );
             context.SaveChanges();
         }

         if (!context.Set<Color>().Any())
         {
             //data seeding
             context.Set<Color>().AddRange(
                 new Color { Name = "Gold" },
                 new Color { Name = "Silver" },
                 new Color { Name = "Black" }
             );
            context.SaveChanges();
         }
          context.SaveChanges();
     }));
    }
}
