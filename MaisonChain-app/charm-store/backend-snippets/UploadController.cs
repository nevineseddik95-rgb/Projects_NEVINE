// Controllers/UploadController.cs  - saves an image and returns its public path.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private static readonly string[] Allowed = { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly IWebHostEnvironment _env;
    public UploadController(IWebHostEnvironment env) => _env = env;

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Post(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Allowed.Contains(ext)) return BadRequest("Only JPG, PNG or WebP images are allowed.");

        var folder = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var stream = System.IO.File.Create(Path.Combine(folder, name)))
            await file.CopyToAsync(stream);

        return Ok(new { url = $"/uploads/{name}" });   // store this string in Product.ImageUrl / Command.ImageUrl
    }
}
