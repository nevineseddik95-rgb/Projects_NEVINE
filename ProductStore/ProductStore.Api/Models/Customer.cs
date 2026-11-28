namespace productStore.Api.Models;

public class Customer
{
    public Guid Id { get; set; }
    private string _email = "";

    public string FullName { get; set; } = "";
    public string Email
    {
        get => _email;
        set => _email = (value ?? "").Trim().ToLowerInvariant();
    }
    public string? Phone { get; set; }
    public bool SmsUpdates { get; set; } = true;
    public string? Address { get; set; }
    public DateTime? LastVisit { get; set; }
}
