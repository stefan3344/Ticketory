namespace Ticketory.Application.Options;

public sealed class OutboxOptions
{
    public int BatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 2;
    public int MaxRetries { get; set; } = 5;
}
