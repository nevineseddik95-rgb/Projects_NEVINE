using System.Net;
using System.Text.Json;

namespace ProductStore.Api.Services;

public sealed class TextBeltSmsSender(
    HttpClient http,
    IConfiguration config,
    ILogger<TextBeltSmsSender> logger) : ISmsSender
{
    public async Task<bool> SendAsync(string toE164, string message)
    {
        var endpoint = config["Sms:TextBeltEndpoint"] ?? "https://textbelt.com/text";
        var apiKey = config["Sms:TextBeltApiKey"] ?? "textbelt";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["phone"] = toE164,
                ["message"] = message,
                ["key"] = apiKey
            });

            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "TextBelt rejected the SMS request with HTTP {StatusCode} for {Recipient}.",
                    (int)response.StatusCode,
                    Mask(toE164));
                return false;
            }

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var success = root.TryGetProperty("success", out var successValue) && successValue.GetBoolean();
            if (success) return true;

            var error = root.TryGetProperty("error", out var errorValue)
                ? errorValue.GetString()
                : "Unknown provider error";
            logger.LogError("TextBelt could not send SMS to {Recipient}: {Error}", Mask(toE164), error);
            return false;
        }
        catch (JsonException)
        {
            logger.LogError("TextBelt returned an unreadable response for {Recipient}.", Mask(toE164));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TextBelt SMS sending failed for {Recipient}.", Mask(toE164));
            return false;
        }
    }

    private static string Mask(string phone) =>
        phone.Length > 4 ? new string('*', phone.Length - 4) + phone[^4..] : "****";
}
