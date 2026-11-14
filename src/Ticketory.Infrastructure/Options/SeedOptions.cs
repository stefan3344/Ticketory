namespace Ticketory.Infrastructure.Options;

public sealed class SeedOptions
{
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string OrganizerEmail { get; set; } = string.Empty;
    public string OrganizerPassword { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPassword { get; set; } = string.Empty;
    public bool DemoData { get; set; } = true;
}
