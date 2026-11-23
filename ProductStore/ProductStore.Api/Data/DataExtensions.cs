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
        var connectionString = BuildConnectionString(builder.Configuration);

        builder.Services.AddNpgsql<ProductStoreContext>(connectionString,
            optionsAction: options => options.UseSeeding((context, _) =>
            {
                if (!context.Set<Genre>().Any())
                {
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
                    context.Set<Size>().AddRange(
                        new Size { Name = "Bold" },
                        new Size { Name = "Thin" }
                    );
                    context.SaveChanges();
                }

                if (!context.Set<Color>().Any())
                {
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

    private static string BuildConnectionString(IConfiguration config)
    {
        // Railway provides DATABASE_URL in URI format:
        //   postgresql://user:password@host:port/dbname
        // Npgsql wants key-value format. Convert it here.
        var envUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (!string.IsNullOrWhiteSpace(envUrl))
            return ConvertUriToNpgsql(envUrl);

        // Local dev fallback (appsettings.json → ConnectionStrings:ProductStore)
        var local = config.GetConnectionString("ProductStore")
            ?? throw new InvalidOperationException(
                "No database configured. Set DATABASE_URL (Railway) or " +
                "ConnectionStrings:ProductStore (appsettings.json).");

        return local;
    }

    private static string ConvertUriToNpgsql(string url)
    {
        // Already key-value format — use as-is.
        if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return url;

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = Npgsql.SslMode.Require,
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }
}