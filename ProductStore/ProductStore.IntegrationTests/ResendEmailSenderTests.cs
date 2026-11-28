using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ProductStore.Api.Services;
using Xunit;

namespace ProductStore.IntegrationTests;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public void Configuration_requires_an_api_key_and_valid_sender()
    {
        var missing = Configuration();
        Assert.False(ResendEmailSender.IsConfigured(missing));
        Assert.False(ResendEmailSender.CanSendToCustomers(missing));

        var invalidSender = Configuration("re_test", "not-an-email");
        Assert.False(ResendEmailSender.IsConfigured(invalidSender));

        var configured = Configuration("re_test", "Beauty & Shine <orders@example.com>");
        Assert.True(ResendEmailSender.IsConfigured(configured));
        Assert.True(ResendEmailSender.CanSendToCustomers(configured));
    }

    [Fact]
    public void Resend_test_sender_is_not_ready_for_customer_delivery()
    {
        var config = Configuration("re_test", "Beauty & Shine <onboarding@resend.dev>");
        Assert.True(ResendEmailSender.IsConfigured(config));
        Assert.False(ResendEmailSender.CanSendToCustomers(config));
    }

    [Fact]
    public async Task Send_posts_the_expected_message_and_authorization()
    {
        RecordingHandler? handler = null;
        using var httpClient = new HttpClient(handler = new RecordingHandler(HttpStatusCode.OK, "{\"id\":\"email-1\"}"))
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var sender = new ResendEmailSender(
            Configuration("re_test", "Beauty & Shine <orders@example.com>"),
            httpClient,
            NullLogger<ResendEmailSender>.Instance);

        var sent = await sender.SendAsync(
            "Customer@Example.com",
            "Test Customer",
            "Order received",
            "<p>Your <strong>order</strong> is ready.</p>");

        Assert.True(sent);
        Assert.NotNull(handler);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.resend.com/emails", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("re_test", handler.AuthorizationParameter);

        using var payload = JsonDocument.Parse(handler.Body!);
        var root = payload.RootElement;
        Assert.Equal("Beauty & Shine <orders@example.com>", root.GetProperty("from").GetString());
        Assert.Equal("customer@example.com", Assert.Single(root.GetProperty("to").EnumerateArray()).GetString());
        Assert.Equal("Order received", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Your <strong>order</strong> is ready.</p>", root.GetProperty("html").GetString());
        Assert.Equal("Your order is ready.", root.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Send_returns_false_when_resend_rejects_the_message()
    {
        using var httpClient = new HttpClient(new RecordingHandler(
            HttpStatusCode.Forbidden,
            "{\"message\":\"The resend.dev sender is restricted to the account owner.\"}"))
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var sender = new ResendEmailSender(
            Configuration("re_test", "Beauty & Shine <onboarding@resend.dev>"),
            httpClient,
            NullLogger<ResendEmailSender>.Instance);

        Assert.False(await sender.SendAsync(
            "customer@example.com", "Customer", "Order received", "<p>Order received.</p>"));
    }

    [Fact]
    public async Task Send_rejects_an_invalid_recipient_without_calling_resend()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"id\":\"unexpected\"}");
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var sender = new ResendEmailSender(
            Configuration("re_test", "orders@example.com"),
            httpClient,
            NullLogger<ResendEmailSender>.Instance);

        Assert.False(await sender.SendAsync("not-an-email", "Customer", "Subject", "<p>Body</p>"));
        Assert.Null(handler.Body);
    }

    private static IConfiguration Configuration(
        string apiKey = "",
        string from = "") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Resend:ApiKey"] = apiKey,
            ["Resend:From"] = from
        }).Build();

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
