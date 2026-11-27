namespace ProductStore.Api.Services;

public static class SmsSettings
{
    public static bool IsEnabled(IConfiguration config)
    {
        var provider = config["Sms:Provider"]?.Trim();
        return string.Equals(provider, "TextBelt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Vonage", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DisabledSmsSender : ISmsSender
{
    public Task<bool> SendAsync(string toE164, string message) => Task.FromResult(false);
}
