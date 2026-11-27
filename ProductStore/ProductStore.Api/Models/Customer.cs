namespace productStore.Api.Models;

public class Customer
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public bool SmsUpdates { get; set; } = true;
    public string? Address { get; set; }
    public DateTime? LastVisit { get; set; }
}
