namespace ProductStore.Api.Models;

public class StoredImage
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
