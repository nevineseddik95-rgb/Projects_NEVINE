using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using productStore.Api.Data;
using ProductStore.Api.Models;

namespace ProductStore.Api.Endpoints;

public static class SeoEndpoints
{
    private const string DefaultOrigin = "https://beautyandshine.up.railway.app";

    public static void UseSearchEngineHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var path = (context.Request.Path.Value ?? "").TrimEnd('/').ToLowerInvariant();
            if (path == "/admin" || path.StartsWith("/admin/", StringComparison.Ordinal) ||
                path is "/cart" or "/checkout" or "/favorites")
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            await next();
        });
    }

    public static void MapSeoEndpoints(this WebApplication app)
    {
        app.MapGet("/sitemap.xml", async (
            ProductStoreContext db,
            IConfiguration configuration,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var origin = CanonicalOrigin(configuration);
            var products = await db.Products.AsNoTracking()
                .OrderBy(p => p.Model)
                .Select(p => new { p.Id, p.ReleaseDate })
                .ToListAsync(cancellationToken);
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "urlset",
                    Url(ns, origin + "/", DateTime.UtcNow.ToString("yyyy-MM-dd"), "weekly", "1.0"),
                    products.Select(product => Url(
                        ns,
                        $"{origin}/product/{product.Id}",
                        product.ReleaseDate.ToString("yyyy-MM-dd"),
                        "weekly",
                        "0.8"))));

            context.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Text(document.ToString(), "application/xml", Encoding.UTF8);
        });

        app.MapGet("/product/{id:guid}", async (
            Guid id,
            ProductStoreContext db,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var product = await db.Products.AsNoTracking()
                .Include(p => p.genre)
                .Include(p => p.Color)
                .Include(p => p.Size)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (product is null) return Results.NotFound();

            var indexPath = Path.Combine(environment.ContentRootPath, "wwwroot", "index.html");
            if (!File.Exists(indexPath)) return Results.NotFound();

            var french = context.Request.Headers.AcceptLanguage.ToString()
                .StartsWith("fr", StringComparison.OrdinalIgnoreCase);
            var origin = CanonicalOrigin(configuration);
            var url = $"{origin}/product/{product.Id}";
            var title = $"{product.Model} | Beauty & Shine";
            var description = french
                ? $"Découvrez {product.Model}, {product.genre?.Name ?? "un bijou"} Beauty & Shine. " +
                  $"{product.Color?.Name ?? ""}{(string.IsNullOrEmpty(product.Size?.Name) ? "" : $", taille {product.Size.Name}")}. " +
                  $"Prix : {product.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} KMF."
                : $"Discover {product.Model}, a {product.genre?.Name ?? "jewelry"} piece from Beauty & Shine. " +
                  $"{product.Color?.Name ?? ""}{(string.IsNullOrEmpty(product.Size?.Name) ? "" : $", size {product.Size.Name}")}. " +
                  $"Price: {product.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} KMF.";
            var image = AbsoluteUrl(origin, product.ImageUrl);
            var structuredData = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Product",
                ["name"] = product.Model,
                ["description"] = description,
                ["image"] = image,
                ["sku"] = product.Id.ToString(),
                ["brand"] = new Dictionary<string, string>
                {
                    ["@type"] = "Brand",
                    ["name"] = "Beauty & Shine"
                },
                ["offers"] = new Dictionary<string, string>
                {
                    ["@type"] = "Offer",
                    ["priceCurrency"] = "KMF",
                    ["price"] = product.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ["availability"] = "https://schema.org/InStock",
                    ["url"] = url
                }
            }).Replace("<", "\\u003c", StringComparison.Ordinal);

            var html = await File.ReadAllTextAsync(indexPath, cancellationToken);
            html = ReplaceTag(html, "<html\\s+lang=\"[^\"]*\"", $"<html lang=\"{(french ? "fr" : "en")}\"");
            html = ReplaceTag(html, "<title>[\\s\\S]*?</title>", $"<title>{WebUtility.HtmlEncode(title)}</title>");
            html = ReplaceTag(html, "<meta\\s+name=\"description\"[^>]*>", Meta("name", "description", description));
            html = ReplaceTag(html, "<meta\\s+name=\"robots\"[^>]*>", Meta("name", "robots", "index, follow, max-image-preview:large"));
            html = ReplaceTag(html, "<link\\s+rel=\"canonical\"[^>]*>", $"<link rel=\"canonical\" href=\"{WebUtility.HtmlEncode(url)}\">");
            html = ReplaceTag(html, "<meta\\s+property=\"og:type\"[^>]*>", Meta("property", "og:type", "product"));
            html = ReplaceTag(html, "<meta\\s+property=\"og:title\"[^>]*>", Meta("property", "og:title", title));
            html = ReplaceTag(html, "<meta\\s+property=\"og:description\"[^>]*>", Meta("property", "og:description", description));
            html = ReplaceTag(html, "<meta\\s+property=\"og:url\"[^>]*>", Meta("property", "og:url", url));
            html = ReplaceTag(html, "<meta\\s+property=\"og:image\"[^>]*>", Meta("property", "og:image", image));
            html = ReplaceTag(html, "<meta\\s+name=\"twitter:title\"[^>]*>", Meta("name", "twitter:title", title));
            html = ReplaceTag(html, "<meta\\s+name=\"twitter:description\"[^>]*>", Meta("name", "twitter:description", description));
            html = ReplaceTag(html, "<meta\\s+name=\"twitter:image\"[^>]*>", Meta("name", "twitter:image", image));
            html = ReplaceTag(
                html,
                "<script\\s+type=\"application/ld\\+json\"\\s+id=\"product-structured-data\">[\\s\\S]*?</script>",
                $"<script type=\"application/ld+json\" id=\"product-structured-data\">{structuredData}</script>");
            html = ReplaceTag(html, "<app-root>\\s*</app-root>", ProductFallback(product, description, image, french));

            context.Response.Headers.CacheControl = "no-cache";
            return Results.Text(html, "text/html", Encoding.UTF8);
        });
    }

    private static string ProductFallback(ProductStore.Api.Models.Product product, string description, string image, bool french)
    {
        var price = product.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var home = french ? "Voir la collection" : "View the collection";
        return $"<app-root><main class=\"shop product-page\"><article class=\"product-detail\">" +
               $"<div class=\"product-detail-media\"><img src=\"{WebUtility.HtmlEncode(image)}\" alt=\"{WebUtility.HtmlEncode(product.Model)}\"></div>" +
               $"<div class=\"product-detail-body\"><h1>{WebUtility.HtmlEncode(product.Model)}</h1>" +
               $"<p class=\"price\">{price} KMF</p><p>{WebUtility.HtmlEncode(description)}</p>" +
               $"<a class=\"btn btn-primary\" href=\"/\">{home}</a></div></article></main></app-root>";
    }

    private static XElement Url(XNamespace ns, string location, string lastModified, string frequency, string priority) =>
        new(ns + "url",
            new XElement(ns + "loc", location),
            new XElement(ns + "lastmod", lastModified),
            new XElement(ns + "changefreq", frequency),
            new XElement(ns + "priority", priority));

    private static string CanonicalOrigin(IConfiguration configuration)
    {
        var configured = configuration["Seo:CanonicalOrigin"]?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? DefaultOrigin : configured.TrimEnd('/');
    }

    private static string AbsoluteUrl(string origin, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return origin + "/placeholder.svg";
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? path
            : origin + (path.StartsWith('/') ? path : "/" + path);
    }

    private static string Meta(string attribute, string name, string content) =>
        $"<meta {attribute}=\"{WebUtility.HtmlEncode(name)}\" content=\"{WebUtility.HtmlEncode(content)}\">";

    private static string ReplaceTag(string html, string pattern, string replacement)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        return regex.IsMatch(html) ? regex.Replace(html, _ => replacement, 1) : html;
    }
}
