using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using productStore.Api.Data;
using productStore.Api.Endpoints;
using ProductStore.Api.Endpoints;
using ProductStore.Api.Models;
using ProductStore.Api.Services;
using Xunit;

namespace ProductStore.IntegrationTests;

public sealed class PayoutAndSeoTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Owner_can_edit_and_delete_viewer_admin_with_database_persistence()
    {
        using var created = await SendJson(HttpMethod.Post, "/admins", "Owner", new
        {
            username = "temporary-viewer",
            password = "InitialPass123!",
            phone = "+237 650 111 222"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdPayload = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdPayload.RootElement.GetProperty("id").GetGuid();

        using var updated = await SendJson(HttpMethod.Put, $"/admins/{id}", "Owner", new
        {
            username = "renamed-viewer",
            password = "UpdatedPass123!",
            phone = "+237650111223"
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var stored = await fixture.FindAdminAsync(id);
        Assert.NotNull(stored);
        Assert.Equal("renamed-viewer", stored.Username);
        Assert.Equal("+237650111223", stored.Phone);
        Assert.Equal("Viewer", stored.Role);
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AdminUser>();
        Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(stored, stored.PasswordHash, "UpdatedPass123!"));

        using var listed = await Send(HttpMethod.Get, "/admins", "Owner");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listedPayload = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        Assert.Contains(listedPayload.RootElement.EnumerateArray(),
            item => item.GetProperty("username").GetString() == "renamed-viewer"
                && item.GetProperty("phone").GetString() == "+237650111223");

        using var deleted = await Send(HttpMethod.Delete, $"/admins/{id}", "Owner");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Null(await fixture.FindAdminAsync(id));
    }

    [Fact]
    public async Task Owner_can_delete_viewer_with_orders_and_keep_commission_history()
    {
        using var created = await SendJson(HttpMethod.Post, "/admins", "Owner", new
        {
            username = "deleted-viewer",
            password = "InitialPass123!",
            phone = "+237650222333"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdPayload = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var adminId = createdPayload.RootElement.GetProperty("id").GetGuid();
        var orderId = await fixture.AddOrderAsync(adminId, 250m, "Pending", 0m);

        try
        {
            using var forbidden = await Send(HttpMethod.Delete, $"/commissions/{orderId}", "Viewer");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

            using var deleted = await Send(HttpMethod.Delete, $"/admins/{adminId}", "Owner");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.Null(await fixture.FindAdminAsync(adminId));

            using var orderResponse = await Send(HttpMethod.Get, $"/commands/{orderId}", "Owner");
            Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);
            var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(order);
            Assert.Null(order.CreatedByAdminId);
            Assert.Equal("deleted-viewer", order.CreatedByAdminName);
            Assert.Equal("Viewer", order.CreatedByAdminRole);

            var commission = Assert.Single(await GetCommissions("Owner"), row => row.OrderId == orderId);
            Assert.Null(commission.AdminId);
            Assert.Equal("deleted-viewer", commission.AdminName);
            Assert.Equal("+237650222333", commission.AdminPhone);

            using var payout = await SendJson(HttpMethod.Post, $"/commissions/{orderId}/payment", "Owner",
                new { amount = 500m, note = "Cash after admin removal" });
            Assert.Equal(HttpStatusCode.OK, payout.StatusCode);
            var sms = fixture.TryTakeSms("+237650222333", order.ReferenceNumber);
            Assert.NotNull(sms);
            Assert.Contains("Commission paid", sms.Message);
            Assert.Contains(order.ReferenceNumber, sms.Message);

            using var removed = await Send(HttpMethod.Delete, $"/commissions/{orderId}", "Owner");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
            Assert.DoesNotContain(await GetCommissions("Owner"), row => row.OrderId == orderId);
            Assert.NotNull(await fixture.FindOrderAsync(orderId));

            using var rejectedPayout = await SendJson(HttpMethod.Post, $"/commissions/{orderId}/payment", "Owner",
                new { amount = 500m });
            Assert.Equal(HttpStatusCode.BadRequest, rejectedPayout.StatusCode);
        }
        finally
        {
            await fixture.RemoveOrderAsync(orderId);
        }
    }

    [Fact]
    public async Task Commission_paid_sms_queues_only_when_payment_becomes_full()
    {
        var orderId = await fixture.AddOrderAsync(ApiFixture.ViewerId, 300m, "Pending", 0m);
        try
        {
            using var orderResponse = await Send(HttpMethod.Get, $"/commands/{orderId}", "Owner");
            var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(order);

            using var partial = await SendJson(HttpMethod.Post, $"/commissions/{orderId}/payment", "Owner",
                new { amount = 250m });
            Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
            Assert.Null(fixture.TryTakeSms("+237650111222", order.ReferenceNumber));

            using var full = await SendJson(HttpMethod.Post, $"/commissions/{orderId}/payment", "Owner",
                new { amount = 500m, note = "Bank transfer" });
            Assert.Equal(HttpStatusCode.OK, full.StatusCode);
            var paidSms = fixture.TryTakeSms("+237650111222", order.ReferenceNumber);
            Assert.NotNull(paidSms);
            Assert.Contains("Commission: 500 KMF", paidSms.Message);
            Assert.Contains("Bank transfer", paidSms.Message);

            using var repeated = await SendJson(HttpMethod.Post, $"/commissions/{orderId}/payment", "Owner",
                new { amount = 500m });
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            Assert.Null(fixture.TryTakeSms("+237650111222", order.ReferenceNumber));

            using var reversed = await SendJson(HttpMethod.Post, $"/commissions/{orderId}/payment", "Owner",
                new { amount = 0m });
            Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
            Assert.Null(fixture.TryTakeSms("+237650111222", order.ReferenceNumber));

            using var repaid = await SendJson(HttpMethod.Post, $"/commissions/{orderId}/payment", "Owner",
                new { amount = 500m });
            Assert.Equal(HttpStatusCode.OK, repaid.StatusCode);
            Assert.NotNull(fixture.TryTakeSms("+237650111222", order.ReferenceNumber));
        }
        finally
        {
            await fixture.RemoveOrderAsync(orderId);
        }
    }

    [Fact]
    public async Task Orders_can_be_deleted_in_every_status()
    {
        var ids = new List<Guid>();
        try
        {
            foreach (var status in new[] { "Pending", "Shipped", "Delivered" })
                ids.Add(await fixture.AddOrderAsync(ApiFixture.ViewerId, 100m, status, 0m));

            foreach (var id in ids)
            {
                using var deleted = await Send(HttpMethod.Delete, $"/commands/{id}", "Viewer");
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
                Assert.Null(await fixture.FindOrderAsync(id));
                using var fetched = await Send(HttpMethod.Get, $"/commands/{id}", "Viewer");
                Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
            }

            using var listed = await Send(HttpMethod.Get, "/commands", "Owner");
            var orders = await listed.Content.ReadFromJsonAsync<List<OrderResponse>>();
            Assert.NotNull(orders);
            Assert.DoesNotContain(orders, order => ids.Contains(order.Id));
        }
        finally
        {
            foreach (var id in ids)
                await fixture.RemoveOrderAsync(id);
        }
    }

    [Fact]
    public async Task Only_owner_can_manage_admins_and_owner_account_is_protected()
    {
        using var viewerEdit = await SendJson(HttpMethod.Put, $"/admins/{ApiFixture.OtherViewerId}", "Viewer", new
        {
            username = "viewer-attempt",
            password = (string?)null
        });
        Assert.Equal(HttpStatusCode.Forbidden, viewerEdit.StatusCode);

        using var viewerDelete = await Send(HttpMethod.Delete, $"/admins/{ApiFixture.OtherViewerId}", "Viewer");
        Assert.Equal(HttpStatusCode.Forbidden, viewerDelete.StatusCode);

        using var ownerEdit = await SendJson(HttpMethod.Put, $"/admins/{ApiFixture.OwnerId}", "Owner", new
        {
            username = "owner-attempt",
            password = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, ownerEdit.StatusCode);

        using var ownerDelete = await Send(HttpMethod.Delete, $"/admins/{ApiFixture.OwnerId}", "Owner");
        Assert.Equal(HttpStatusCode.BadRequest, ownerDelete.StatusCode);

        var owner = await fixture.FindAdminAsync(ApiFixture.OwnerId);
        Assert.NotNull(owner);
        Assert.Equal("owner", owner.Username);
        Assert.Equal("Owner", owner.Role);
    }

    [Fact]
    public async Task Owner_can_record_partial_full_and_reverse_a_payout()
    {
        var initial = await GetCommissions("Owner");
        var row = Assert.Single(initial, row => row.Status == "Pending");
        Assert.Equal("Unpaid", row.PayoutStatus);
        Assert.Equal(500m, row.OutstandingAmount);

        var partial = await SendJson(HttpMethod.Post, $"/commissions/{row.OrderId}/payment", "Owner",
            new { amount = 2m, note = "Cash partial" });
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var partialBody = await partial.Content.ReadFromJsonAsync<CommissionResponse>();
        Assert.NotNull(partialBody);
        Assert.Equal(2m, partialBody.PaidAmount);
        Assert.Equal(498m, partialBody.OutstandingAmount);
        Assert.Equal("PartiallyPaid", partialBody.PayoutStatus);
        Assert.NotNull(partialBody.PaidAt);
        Assert.Equal("Cash partial", partialBody.PaymentNote);

        var full = await SendJson(HttpMethod.Post, $"/commissions/{row.OrderId}/payment", "Owner",
            new { amount = 500m, note = "Cash complete" });
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        var fullBody = await full.Content.ReadFromJsonAsync<CommissionResponse>();
        Assert.NotNull(fullBody);
        Assert.Equal("Paid", fullBody.PayoutStatus);
        Assert.Equal(0m, fullBody.OutstandingAmount);

        var reversed = await SendJson(HttpMethod.Post, $"/commissions/{row.OrderId}/payment", "Owner",
            new { amount = 0m, note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, reversed.StatusCode);
        var reversedBody = await reversed.Content.ReadFromJsonAsync<CommissionResponse>();
        Assert.NotNull(reversedBody);
        Assert.Equal("Unpaid", reversedBody.PayoutStatus);
        Assert.Equal(0m, reversedBody.PaidAmount);
        Assert.Equal(500m, reversedBody.OutstandingAmount);
        Assert.Null(reversedBody.PaidAt);
    }

    [Fact]
    public async Task Payout_validation_rejects_invalid_amounts_and_long_notes()
    {
        var row = Assert.Single(await GetCommissions("Owner"), row => row.Status == "Pending");
        foreach (var body in new object[]
                 {
                     new { amount = -0.01m },
                     new { amount = 500.01m },
                     new { note = "Missing amount" },
                     new { amount = 1m, note = new string('x', 1001) }
                 })
        {
            var response = await SendJson(HttpMethod.Post, $"/commissions/{row.OrderId}/payment", "Owner", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var unchanged = Assert.Single(await GetCommissions("Owner"), row => row.Status == "Pending");
        Assert.Equal(0m, unchanged.PaidAmount);
        Assert.Equal("Unpaid", unchanged.PayoutStatus);
    }

    [Fact]
    public async Task Viewer_cannot_record_payouts_and_sees_only_own_commissions()
    {
        var rows = await GetCommissions("Viewer");
        var row = Assert.Single(rows);
        Assert.Equal(ApiFixture.ViewerId, row.AdminId);

        var response = await SendJson(HttpMethod.Post, $"/commissions/{row.OrderId}/payment", "Viewer",
            new { amount = 1m });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancelled_order_is_excluded_and_cannot_receive_a_payout()
    {
        var rows = await GetCommissions("Owner");
        var cancelled = Assert.Single(rows, row => row.Status == "Cancelled");
        Assert.False(cancelled.Eligible);
        Assert.Equal("Excluded", cancelled.PayoutStatus);
        Assert.Equal(0m, cancelled.CommissionAmount);
        Assert.Equal(0m, cancelled.OutstandingAmount);

        var response = await SendJson(HttpMethod.Post, $"/commissions/{cancelled.OrderId}/payment", "Owner",
            new { amount = 1m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Commission_edits_enforce_owner_defaults_and_paid_amount_limits()
    {
        var orderId = await fixture.AddOrderAsync(ApiFixture.ViewerId, 100m, "Pending", 3m);
        try
        {
            var dto = ApiFixture.OrderSave(10m, 2m);
            var negative = await SendJson(HttpMethod.Put, $"/commands/{orderId}", "Owner",
                dto with { CommissionAmount = -1m });
            Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

            var rejected = await SendJson(HttpMethod.Put, $"/commands/{orderId}", "Owner", dto);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

            var accepted = await SendJson(HttpMethod.Put, $"/commands/{orderId}", "Owner",
                dto with { Total = 200m, CommissionAmount = 10m });
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

            var rows = await GetCommissions("Owner");
            var updated = Assert.Single(rows, row => row.OrderId == orderId);
            Assert.Equal(10m, updated.CommissionAmount);
            Assert.Equal(3m, updated.PaidAmount);
            Assert.Equal("PartiallyPaid", updated.PayoutStatus);

            var reset = await SendJson(HttpMethod.Put, $"/commands/{orderId}", "Owner",
                dto with { Total = 300m, CommissionAmount = null });
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
            var resetRow = Assert.Single(await GetCommissions("Owner"), row => row.OrderId == orderId);
            Assert.Equal(500m, resetRow.CommissionAmount);
            Assert.Equal(497m, resetRow.OutstandingAmount);
        }
        finally
        {
            await fixture.RemoveOrderAsync(orderId);
        }
    }

    [Fact]
    public async Task Viewer_creation_cannot_override_the_default_commission()
    {
        using var response = await SendJson(HttpMethod.Post, "/commands", "Viewer", ApiFixture.OrderSave(12500m, 1m));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        try
        {
            Assert.Equal(500m, order.CommissionAmount);
            Assert.Equal("Viewer", order.CreatedByAdminRole);
            Assert.NotNull(fixture.TryTakeEmail("customer@example.com"));
        }
        finally
        {
            await fixture.RemoveOrderAsync(order.Id);
        }
    }

    [Fact]
    public async Task Owner_creation_has_no_commission_and_only_owner_can_edit_staff_commission()
    {
        using var created = await SendJson(HttpMethod.Post, "/commands", "Owner", ApiFixture.OrderSave(12500m, 900m));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ownerOrder = await created.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(ownerOrder);
        try
        {
            Assert.Equal(0m, ownerOrder.CommissionAmount);
            Assert.NotNull(fixture.TryTakeEmail("customer@example.com"));
        }
        finally
        {
            await fixture.RemoveOrderAsync(ownerOrder.Id);
        }

        var staffOrderId = await fixture.AddOrderAsync(ApiFixture.ViewerId, 12500m, "Pending", 0m);
        try
        {
            var ownerEdit = await SendJson(HttpMethod.Put, $"/commands/{staffOrderId}", "Owner",
                ApiFixture.OrderSave(15000m, 725m));
            Assert.Equal(HttpStatusCode.NoContent, ownerEdit.StatusCode);

            var viewerEdit = await SendJson(HttpMethod.Put, $"/commands/{staffOrderId}", "Viewer",
                ApiFixture.OrderSave(16000m, 1m));
            Assert.Equal(HttpStatusCode.NoContent, viewerEdit.StatusCode);

            var updated = Assert.Single(await GetCommissions("Owner"), row => row.OrderId == staffOrderId);
            Assert.Equal(725m, updated.CommissionAmount);
            Assert.Equal(16000m, updated.OrderTotal);
        }
        finally
        {
            await fixture.RemoveOrderAsync(staffOrderId);
        }
    }

    [Fact]
    public async Task Admin_created_order_queues_a_customer_email()
    {
        using var response = await SendJson(HttpMethod.Post, "/commands", "Viewer", new
        {
            customerId = ApiFixture.CustomerId,
            productId = ApiFixture.ProductId,
            genreId = ApiFixture.GenreId,
            colorId = ApiFixture.ColorId,
            sizeId = ApiFixture.SizeId,
            deliveryAddress = "Douala",
            date = new DateOnly(2026, 11, 28),
            total = 12500m,
            note = (string?)null,
            imageUrl = "/uploads/golden.jpg",
            depositAmount = 5000m,
            paymentReference = (string?)null,
            trackingNumber = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var email = fixture.TryTakeEmail("customer@example.com");
        Assert.NotNull(email);
        Assert.Equal("Test Customer", email.ToName);
        Assert.Contains(order.ReferenceNumber, email.Subject);
        Assert.Contains("Golden &lt;Bracelet&gt;", email.HtmlBody);
        Assert.Contains("Douala", email.HtmlBody);
    }

    [Fact]
    public async Task Checkout_allows_same_email_with_different_names_and_details()
    {
        const string email = "repeat@example.com";
        var first = await SendJson(HttpMethod.Post, "/checkout", "Owner", new
        {
            fullName = "Alice One",
            email,
            phone = "+269 321 2345",
            smsUpdates = false,
            deliveryAddress = "Douala",
            depositAmount = 5000m,
            paymentMethod = "MVola",
            items = new[] { new { productId = ApiFixture.ProductId, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await SendJson(HttpMethod.Post, "/checkout", "Owner", new
        {
            fullName = "Bob Two",
            email,
            phone = "+269 321 2346",
            smsUpdates = false,
            deliveryAddress = "Yaounde",
            depositAmount = 5000m,
            paymentMethod = "MVola",
            items = new[] { new { productId = ApiFixture.ProductId, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var customersResponse = await Send(HttpMethod.Get, "/customers", "Owner");
        Assert.Equal(HttpStatusCode.OK, customersResponse.StatusCode);
        var customers = await customersResponse.Content.ReadFromJsonAsync<List<productStore.Api.Models.Customer>>();
        Assert.NotNull(customers);
        var matching = customers.Where(c => c.Email == email).OrderBy(c => c.FullName).ToList();
        Assert.Equal(new[] { "Alice One", "Bob Two" }, matching.Select(c => c.FullName));
        Assert.Equal("Douala", matching[0].Address);
        Assert.Equal("Yaounde", matching[1].Address);
        Assert.NotEqual(matching[0].Id, matching[1].Id);

        using var ordersResponse = await Send(HttpMethod.Get, "/commands", "Owner");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
        var orders = await ordersResponse.Content.ReadFromJsonAsync<List<OrderResponse>>();
        Assert.NotNull(orders);
        Assert.Contains(orders, order => order.CustomerName == "Alice One" && order.DeliveryAddress == "Douala");
        Assert.Contains(orders, order => order.CustomerName == "Bob Two" && order.DeliveryAddress == "Yaounde");
    }

    [Fact]
    public async Task Sitemap_contains_public_home_and_product_urls()
    {
        using var response = await fixture.Client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locations = xml.Descendants(ns + "loc").Select(item => item.Value).ToList();
        Assert.Contains("https://example.test/", locations);
        Assert.Contains($"https://example.test/product/{ApiFixture.ProductId}", locations);
    }

    [Fact]
    public async Task Product_html_defaults_to_french_with_localized_metadata_json_ld_and_crawler_text()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/product/{ApiFixture.ProductId}");
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<html lang=\"fr\"", html);
        Assert.Contains("<title>Golden &lt;Bracelet&gt; | Beauty &amp; Shine</title>", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://example.test/product/", html);
        var descriptionTag = Regex.Match(html, "<meta name=\"description\"[^>]*>").Value;
        Assert.True(html.Contains("D&#233;couvrez Golden &lt;Bracelet&gt;"), descriptionTag);
        Assert.Contains("<app-root><main class=\"shop product-page\">", html);
        Assert.Contains("<h1>Golden &lt;Bracelet&gt;</h1>", html);
        Assert.Contains("Voir la collection", html);
        Assert.DoesNotContain("<title>Golden <Bracelet>", html);

        var match = Regex.Match(html,
            "<script type=\"application/ld\\+json\" id=\"product-structured-data\">([\\s\\S]*?)</script>");
        Assert.True(match.Success);
        using var structured = JsonDocument.Parse(match.Groups[1].Value);
        Assert.True(structured.RootElement.TryGetProperty("@type", out _), match.Groups[1].Value);
        Assert.Equal("Product", structured.RootElement.GetProperty("@type").GetString());
        Assert.Equal("Golden <Bracelet>", structured.RootElement.GetProperty("name").GetString());
        Assert.Equal("12500.00", structured.RootElement.GetProperty("offers").GetProperty("price").GetString());
        Assert.Equal("KMF", structured.RootElement.GetProperty("offers").GetProperty("priceCurrency").GetString());
    }

    [Fact]
    public async Task Product_html_honors_an_english_accept_language_header()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/product/{ApiFixture.ProductId}");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<html lang=\"en\"", html);
        Assert.Contains("Discover Golden &lt;Bracelet&gt;", html);
        Assert.Contains("View the collection", html);
    }

    [Fact]
    public async Task Product_endpoint_returns_navigation_names_for_the_client_page()
    {
        using var response = await fixture.Client.GetAsync($"/api/products/{ApiFixture.ProductId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var product = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(product);
        var item = product.RootElement;
        Assert.Equal("Bracelet", item.GetProperty("genre").GetString());
        Assert.Equal("Gold", item.GetProperty("color").GetString());
        Assert.Equal("Medium", item.GetProperty("size").GetString());
    }

    [Fact]
    public async Task Private_pages_have_noindex_headers_and_public_pages_do_not()
    {
        foreach (var path in new[] { "/admin", "/admin/orders", "/cart/", "/checkout", "/favorites" })
        {
            using var response = await fixture.Client.GetAsync(path);
            Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        }

        using var publicResponse = await fixture.Client.GetAsync("/robots.txt");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        Assert.False(publicResponse.Headers.Contains("X-Robots-Tag"));
        var robots = await publicResponse.Content.ReadAsStringAsync();
        Assert.Contains("Sitemap: https://beautyandshine.up.railway.app/sitemap.xml", robots);
        Assert.Contains("Disallow: /admin", robots);
    }

    [Fact]
    public void Commission_projection_translates_with_the_postgresql_provider()
    {
        var options = new DbContextOptionsBuilder<ProductStoreContext>()
            .UseNpgsql("Host=localhost;Database=verification;Username=verification;Password=verification")
            .Options;
        using var db = new ProductStoreContext(options);
        var method = typeof(CommandsEndpoints).GetMethod("QueryCommissions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var query = (IQueryable<CommissionResponse>)method.Invoke(null,
            new object?[] { db, ApiFixture.ViewerId, ApiFixture.ViewerOrderId })!;
        var sql = query.ToQueryString();
        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains("\"CommissionPaidAmount\"", sql);
        Assert.Contains("\"CommissionPaymentNote\"", sql);
    }

    private async Task<List<CommissionResponse>> GetCommissions(string role)
    {
        using var response = await Send(HttpMethod.Get, "/commissions", role);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<List<CommissionResponse>>();
        Assert.NotNull(rows);
        return rows;
    }

    private async Task<HttpResponseMessage> SendJson(HttpMethod method, string path, string role, object body)
    {
        using var request = CreateRequest(method, path, role);
        request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string role)
    {
        using var request = CreateRequest(method, path, role);
        return await fixture.Client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string role)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-Role", role);
        request.Headers.Add("X-Test-Id", (role == "Owner" ? ApiFixture.OwnerId : ApiFixture.ViewerId).ToString());
        return request;
    }
}

public sealed class ApiFixture : IAsyncLifetime
{
    public static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ViewerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OtherViewerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid CustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid GenreId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid ColorId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid SizeId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid ProductId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid ViewerOrderId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    public HttpClient Client { get; private set; } = null!;
    public EmailNotificationQueue EmailQueue { get; private set; } = null!;
    public SmsNotificationQueue SmsQueue { get; private set; } = null!;
    private WebApplication app = null!;
    private SqliteConnection connection = null!;
    private string contentRoot = null!;

    public async Task InitializeAsync()
    {
        contentRoot = Path.Combine(Path.GetTempPath(), $"beauty-shine-tests-{Guid.NewGuid():N}");
        var wwwroot = Path.Combine(contentRoot, "wwwroot");
        Directory.CreateDirectory(wwwroot);
        File.Copy(FindFrontendFile("index.html"), Path.Combine(wwwroot, "index.html"));
        File.Copy(FindFrontendFile("robots.txt"), Path.Combine(wwwroot, "robots.txt"));

        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            EnvironmentName = "Testing"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seo:CanonicalOrigin"] = "https://example.test",
            ["Sms:Provider"] = "Vonage"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<ProductStoreContext>(options => options.UseSqlite(connection));
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Owner", policy => policy.RequireRole("Owner"));
            options.AddPolicy("Staff", policy => policy.RequireRole("Owner", "Viewer"));
        });
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<AdminChangeNotifier>();
        builder.Services.AddRateLimiter(options =>
            options.AddPolicy("checkout", _ => RateLimitPartition.GetNoLimiter<string>("test")));
        builder.Services.AddSingleton<EmailNotificationQueue>();
        builder.Services.AddSingleton<SmsNotificationQueue>();
        builder.Services.AddSingleton<OrderNotifications>();

        app = builder.Build();
        EmailQueue = app.Services.GetRequiredService<EmailNotificationQueue>();
        SmsQueue = app.Services.GetRequiredService<SmsNotificationQueue>();
        app.UsePathBase("/api");
        app.UseRouting();
        app.UseSearchEngineHeaders();
        app.UseStaticFiles();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAdminsEndpoints();
        app.MapCommandsEndpoints();
        app.MapCheckoutEndpoints();
        app.MapCustomersEndpoints();
        app.MapProductsEndpoints();
        app.MapSeoEndpoints();
        await app.StartAsync();
        Client = app.GetTestClient();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
        await db.Database.EnsureCreatedAsync();
        db.AddRange(
            new AdminUser { Id = OwnerId, Username = "owner", Role = "Owner" },
            new AdminUser { Id = ViewerId, Username = "staff-one", Role = "Viewer", Phone = "+237650111222" },
            new AdminUser { Id = OtherViewerId, Username = "staff-two", Role = "Viewer", Phone = "+237650333444" },
            new productStore.Api.Models.Customer { Id = CustomerId, FullName = "Test Customer", Email = "CUSTOMER@Example.com" },
            new Genre { Id = GenreId, Name = "Bracelet" },
            new Color { Id = ColorId, Name = "Gold" },
            new Size { Id = SizeId, Name = "Medium" },
            new Product
            {
                Id = ProductId,
                Model = "Golden <Bracelet>",
                GenreId = GenreId,
                ColorId = ColorId,
                SizeId = SizeId,
                Price = 12500m,
                ReleaseDate = new DateOnly(2026, 11, 1),
                ImageUrl = "/uploads/golden.jpg"
            });
        db.Commands.AddRange(
            Command(ViewerOrderId, "BS-VIEWER", ViewerId, 100.20m, "Pending", 500m),
            Command(Guid.NewGuid(), "BS-OWNER", OwnerId, 200m, "Pending", 0m),
            Command(Guid.NewGuid(), "BS-CANCELLED", OtherViewerId, 40m, "Cancelled", 500m));
        await db.SaveChangesAsync();
    }

    public EmailNotification? TryTakeEmail(string recipient)
    {
        while (EmailQueue.TryRead(out var notification))
        {
            if (notification.ToEmail == recipient) return notification;
        }
        return null;
    }

    public SmsNotification? TryTakeSms(string recipient, string? reference = null)
    {
        while (SmsQueue.TryRead(out var notification))
        {
            if (notification.ToPhone == recipient
                && (reference is null || notification.Message.Contains(reference, StringComparison.Ordinal)))
                return notification;
        }
        return null;
    }

    public async Task<AdminUser?> FindAdminAsync(Guid id)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
        return await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id);
    }

    public async Task<productStore.Api.Models.Command?> FindOrderAsync(Guid id)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
        return await db.Commands.AsNoTracking().FirstOrDefaultAsync(command => command.Id == id);
    }

    public async Task<Guid> AddOrderAsync(Guid adminId, decimal total, string status, decimal paidAmount)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
        var command = Command(Guid.NewGuid(), $"BS-{Guid.NewGuid():N}"[..14], adminId, total, status,
            adminId == OwnerId ? 0m : 500m);
        var admin = await db.AdminUsers.AsNoTracking().SingleOrDefaultAsync(user => user.Id == adminId);
        command.CreatedByAdminName = admin?.Username;
        command.CreatedByAdminRole = admin?.Role;
        command.CreatedByAdminPhone = admin?.Phone;
        command.CommissionPaidAmount = paidAmount;
        command.CommissionPaidAt = paidAmount > 0 ? DateTime.UtcNow : null;
        db.Commands.Add(command);
        await db.SaveChangesAsync();
        return command.Id;
    }

    public async Task RemoveOrderAsync(Guid orderId)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductStoreContext>();
        var command = await db.Commands.FindAsync(orderId);
        if (command is not null) db.Commands.Remove(command);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (app is not null) await app.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
        if (!string.IsNullOrEmpty(contentRoot) && Directory.Exists(contentRoot))
            Directory.Delete(contentRoot, true);
    }

    public static SaveCommand OrderSave(decimal total, decimal? commissionAmount = null) => new(
        CustomerId,
        ProductId,
        GenreId,
        ColorId,
        SizeId,
        "Douala",
        new DateOnly(2026, 11, 28),
        total,
        null,
        "/uploads/test.jpg",
        0m,
        CommissionAmount: commissionAmount);

    private static productStore.Api.Models.Command Command(Guid id, string reference, Guid adminId, decimal total, string status,
        decimal commission) => new()
    {
        Id = id,
        ReferenceNumber = reference,
        CustomerId = CustomerId,
        CreatedByAdminId = adminId,
        CreatedByAdminName = adminId == OwnerId ? "owner" : adminId == ViewerId ? "staff-one" : "staff-two",
        CreatedByAdminRole = adminId == OwnerId ? "Owner" : "Viewer",
        CreatedByAdminPhone = adminId == ViewerId ? "+237650111222" : adminId == OtherViewerId ? "+237650333444" : null,
        ProductId = ProductId,
        Model = "Golden Bracelet",
        GenreId = GenreId,
        ColorId = ColorId,
        SizeId = SizeId,
        DeliveryAddress = "Douala",
        Date = new DateOnly(2026, 11, 28),
        Total = total,
        Status = status,
        CommissionAmount = commission
    };

    private static string FindFrontendFile(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "MaisonChain-app", "charm-store", "dist",
                "charm-store", "browser", name);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Build the Angular application before running integration tests. Missing {name}.");
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            var id = Request.Headers["X-Test-Id"].ToString();
            if (string.IsNullOrWhiteSpace(role) || !Guid.TryParse(id, out _))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
