using System.Net.Http.Headers;
using System.Text;
using Vonage;
using Vonage.Request;
using Vonage.Messaging;
using Vonage.Messages.Sms;

namespace ProductStore.Api.Services;

public interface ISmsSender
{
    /// returns true when the provider accepted the message
    Task<bool> SendAsync(string toE164, string message);
}

public static class PhoneNumber
{
    /// <summary>
    /// Normalizes a phone number to E.164 (e.g. "+201007005884").
    /// Returns null if the input cannot be a phone number.
    ///
    ///   "0100 700 5884"   → "+201007005884"   (cc = "20", Egypt)
    ///   "1007005884"      → "+201007005884"
    ///   "+20 100 700 5884"→ "+201007005884"
    ///   "0020 100 700 5884"→ "+201007005884"
    /// </summary>
    public static string? Normalize(string? input, string countryCode = "20")
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Keep only '+' and digits. Unicode digits (e.g. ٠١٢) are converted to ASCII.
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

        // A '+' is only legal as the very first character.
        if (s.IndexOf('+', 1) >= 0)
            return null;

        string result;

        if (s[0] == '+')
        {
            // Already in international form.
            result = s;
        }
        else if (s.StartsWith("00", StringComparison.Ordinal))
        {
            // "00…" is the international access prefix.
            result = "+" + s[2..];
        }
        else
        {
            // Local Egyptian number: prepend +20 and drop the trunk '0'.
            var cc = OnlyDigits(countryCode).TrimStart('0');
            if (cc.Length == 0)
                return null;

            // Egypt uses '0' as the national trunk prefix:
            //   "01007005884"  → "+201007005884"
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
        if (digits.Length < 8 || digits.Length > 15)   // E.164 max = 15 digits
            return false;

        for (int i = 0; i < digits.Length; i++)
            if (digits[i] < '0' || digits[i] > '9')
                return false;

        if (digits[0] == '0')                          // country codes never start with 0
            return false;

        return true;
    }
}

public class TwilioSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TwilioSmsSender> _log;

    public TwilioSmsSender(HttpClient http, IConfiguration config, ILogger<TwilioSmsSender> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    public async Task<bool> SendAsync(string toE164, string message)
    {
        var apiKey = _config["Sms:ApiKey"];
var apiSecret = _config["Sms:ApiSecret"];
var from = _config["Sms:From"] ?? "VonageAPIs";

var credentials = Credentials.FromApiKeyAndSecret(apiKey, apiSecret);
var vonageClient = new VonageClient(credentials);
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret) || string.IsNullOrWhiteSpace(from))
        {
            _log.LogWarning("SMS: Sms:AccountSid, Sms:AuthToken or Sms:From is not set, so no text message was sent.");
            return false;
        }

        try
        {
            // using var req = new HttpRequestMessage(HttpMethod.Post,
            //     $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json");
            // req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            //     Convert.ToBase64String(Encoding.ASCII.GetBytes($"{sid}:{token}")));
            // req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            // {
            //     ["To"] = to, ["From"] = from, ["Body"] = message
            // });

            // using var res = await _http.SendAsync(req);
            


            var res = await vonageClient.SmsClient.SendAnSmsAsync(new SendSmsRequest
            {
                To = toE164,
                From = from,
                Text = "This is an SMS text message sent using the Vonage SMS API."
            });
            var firstMessage = res.Messages.FirstOrDefault();

            if (firstMessage?.Status == "0")
            {
                return true;
            }

             _log.LogError(
        "SMS: the provider refused the message to {To} ({Status}): {Body}",
        Mask(toE164),
        firstMessage?.Status ?? "Unknown",
        firstMessage?.ErrorText ?? "Unknown error"
    );
            return false;
        }
        catch (Exception ex)
        {
            _log.LogError("SMS: sending failed with {Type}: {Message}", ex.GetType().Name, ex.Message);
            return false;
        }
    }

    private static string Mask(string n) => n.Length > 4 ? new string('*', n.Length - 4) + n[^4..] : "****";
}