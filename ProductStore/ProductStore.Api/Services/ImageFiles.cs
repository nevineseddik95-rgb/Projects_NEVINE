namespace ProductStore.Api.Services;

public static class ImageFiles
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    public static string? ContentTypeFor(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return ContentTypes.TryGetValue(extension, out var contentType) ? contentType : null;
    }

    public static string? DetectContentType(byte[] data, string fileName)
    {
        var expected = ContentTypeFor(fileName);
        if (expected is null) return null;

        var valid = expected switch
        {
            "image/jpeg" => data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
            "image/png" => data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 &&
                data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D && data[5] == 0x0A &&
                data[6] == 0x1A && data[7] == 0x0A,
            "image/webp" => data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 &&
                data[2] == 0x46 && data[3] == 0x46 && data[8] == 0x57 && data[9] == 0x45 &&
                data[10] == 0x42 && data[11] == 0x50,
            _ => false
        };
        return valid ? expected : null;
    }
}
