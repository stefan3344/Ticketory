namespace Ticketory.Application.Options;

public sealed class LockOptions
{
    public int ExpirySeconds { get; set; } = 30;
    public int WaitSeconds { get; set; } = 5;
    public int RetryDelayMilliseconds { get; set; } = 100;
}
