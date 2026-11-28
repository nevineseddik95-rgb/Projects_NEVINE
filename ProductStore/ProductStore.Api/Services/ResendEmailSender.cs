using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace ProductStore.Api.Services;

public sealed class ResendEmailSender(
    IConfiguration config,
    HttpClient httpClient,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public static bool IsConfigured(IConfiguration config)
    {
        var apiKey = config["Resend:ApiKey"]?.Trim();
        var from = config["Resend:From"];
        return !string.IsNullOrWhiteSpace(apiKey)
            && EmailAddress.TryNormalize(from, out var address)
            && address.Length > 0;
    }

    public static bool CanSendToCustomers(IConfiguration config)
    {
        if (!IsConfigured(config)) return false;
        var from = MailAddress.TryCreate(config["Resend:From"]?.Trim(), out var address)
            ? address.Address
            : "";
        var domain = from.Split('@').LastOrDefault() ?? "";
        return !domain.Equals("resend.dev", StringComparison.OrdinalIgnoreCase)
            && !domain.EndsWith(".resend.dev", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(config))
        {
            logger.LogError("Resend is not configured. Set Resend__ApiKey and Resend__From.");
            return false;
        }
        if (!EmailAddress.TryNormalize(toEmail, out var recipient) || recipient.Length == 0)
        {
            logger.LogError("The customer email address is missing or invalid.");
            return false;
        }
        if (!EmailAddress.TryNormalize(config["Resend:From"], out _))
        {
            logger.LogError("Resend__From is invalid.");
            return false;
        }

        var textBody = WebUtility.HtmlDecode(Regex.Replace(htmlBody, "<[^>]+>", " "));
        textBody = Regex.Replace(textBody, "\\s+", " ").Trim();
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = config["Resend:From"]!.Trim(),
                to = new[] { recipient },
                subject,
                html = htmlBody,
                text = textBody.Length > 0 ? textBody : subject
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", config["Resend:ApiKey"]!.Trim());
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return true;

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Resend rejected the email. Status {StatusCode}: {Response}",
                (int)response.StatusCode,
                error.Length > 2000 ? error[..2000] : error);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Resend email delivery timed out.");
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError("Resend email delivery failed: {Message}", ex.Message);
            return false;
        }
    }
}
