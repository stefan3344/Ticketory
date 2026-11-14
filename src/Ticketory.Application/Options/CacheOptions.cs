namespace Ticketory.Application.Options;

public sealed class CacheOptions
{
    public int EventListTtlSeconds { get; set; } = 30;
    public int EventDetailsTtlSeconds { get; set; } = 120;
    public int DashboardTtlSeconds { get; set; } = 15;
    public int VersionTtlDays { get; set; } = 365;
}
