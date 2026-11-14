namespace Ticketory.Application.Common;

public static class CacheKeys
{
    public const string EventsVersion = "events:version";

    public static string EventDetails(Guid eventId) => $"event:{eventId}";

    public static string PublishedEvents(string version, string? search, int page, int pageSize)
        => $"events:{version}:{search?.Trim().ToLowerInvariant()}:{page}:{pageSize}";

    public static string Dashboard => "dashboard:stats:v2";
}
