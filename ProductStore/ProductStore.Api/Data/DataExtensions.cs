using Microsoft.EntityFrameworkCore;
using ProductStore.Api.Models;
using ProductStore.Api.Services;

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

    public static async Task SeedStoredImagesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var folder = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        if (!Directory.Exists(folder)) return;

        var existing = await db.StoredImages.Select(i => i.FileName).ToListAsync();
        var known = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var additions = new List<StoredImage>();
        foreach (var path in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(path);
            if (known.Contains(fileName)) continue;
            var data = await File.ReadAllBytesAsync(path);
            var contentType = ImageFiles.DetectContentType(data, fileName);
            if (contentType is null) continue;
            additions.Add(new StoredImage
            {
                FileName = fileName,
                ContentType = contentType,
                Data = data,
                CreatedAt = File.GetCreationTimeUtc(path)
            });
        }
        if (additions.Count > 0)
        {
            db.StoredImages.AddRange(additions);
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Stored {Count} existing upload images in the database.", additions.Count);
        }
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