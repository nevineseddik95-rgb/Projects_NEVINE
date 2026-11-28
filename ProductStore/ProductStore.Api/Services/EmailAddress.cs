using System.Net.Mail;

namespace ProductStore.Api.Services;

public static class EmailAddress
{
    public static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = "";
        var text = (value ?? "").Trim();
        if (text.Length == 0) return true;
        if (text.Length > 254 || !MailAddress.TryCreate(text, out var address) || string.IsNullOrWhiteSpace(address.Address))
            return false;

        normalized = address.Address.Trim().ToLowerInvariant();
        return true;
    }
}
