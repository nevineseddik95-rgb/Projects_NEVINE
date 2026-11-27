using System.Text;
using Vonage;
using Vonage.Request;
using Vonage.Messaging;
using Vonage.Messages.Sms;

namespace ProductStore.Api.Services;

public interface ISmsSender
{
    Task<bool> SendAsync(string toE164, string message);
}

public static class PhoneNumber
{
    public static string? Normalize(string? input, string countryCode = "20")
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c == '+')
                sb.Append('+');
            else if (char.IsDigit(c))
            {
                var n = (int)char.GetNumericValue(c);
                if (n >= 0 && n <= 9)
                    sb.Append((char)('0' + n));
            }
        }

        var s = sb.ToString();
        if (s.Length == 0)
            return null;
        if (s.IndexOf('+', 1) >= 0)
            return null;

        string result;
        if (s[0] == '+')
        {
            result = s;
        }
        else if (s.StartsWith("00", StringComparison.Ordinal))
        {
            result = "+" + s[2..];
        }
        else
        {
            var cc = OnlyDigits(countryCode).TrimStart('0');
            if (cc.Length == 0)
                return null;

            var local = s[0] == '0' ? s[1..] : s;
            if (local.Length == 0)
                return null;

            result = "+" + cc + local;
        }

        return IsE164(result) ? result : null;
    }

    private static string OnlyDigits(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            if (c >= '0' && c <= '9')
                sb.Append(c);
        return sb.ToString();
    }

    private static bool IsE164(string s)
    {
        if (s.Length < 2 || s[0] != '+')
            return false;

        var digits = s.AsSpan(1);
        if (digits.Length < 8 || digits.Length > 15)
            return false;

        for (var i = 0; i < digits.Length; i++)
            if (digits[i] < '0' || digits[i] > '9')
                return false;

        return digits[0] != '0';
    }
}

public sealed class VonageSmsSender(
    IConfiguration config,
    ILogger<VonageSmsSender> logger) : ISmsSender
{
    public async Task<bool> SendAsync(string toE164, string message)
    {
        var apiKey = config["Sms:ApiKey"];
        var apiSecret = config["Sms:ApiSecret"];
        var from = config["Sms:From"] ?? "BeautyAndShine";

        if (string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(apiSecret)
            || string.IsNullOrWhiteSpace(from))
        {
            logger.LogWarning("SMS provider is Vonage but its API key, secret, or sender is not configured.");
            return false;
        }

        try
        {
            var credentials = Credentials.FromApiKeyAndSecret(apiKey, apiSecret);
            var client = new VonageClient(credentials);
            var response = await client.SmsClient.SendAnSmsAsync(new SendSmsRequest
            {
                To = toE164,
                From = from,
                Text = message
            });

            var firstMessage = response.Messages.FirstOrDefault();
            if (firstMessage?.Status == "0")
                return true;

            logger.LogError(
                "SMS provider refused the message to {Recipient} ({Status}): {Error}",
                Mask(toE164),
                firstMessage?.Status ?? "Unknown",
                firstMessage?.ErrorText ?? "Unknown error");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS sending failed for {Recipient}.", Mask(toE164));
            return false;
        }
    }

    private static string Mask(string phone) =>
        phone.Length > 4 ? new string('*', phone.Length - 4) + phone[^4..] : "****";
}
