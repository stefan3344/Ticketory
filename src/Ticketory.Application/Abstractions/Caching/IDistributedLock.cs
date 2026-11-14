namespace Ticketory.Application.Abstractions.Caching;

public interface IDistributedLock
{
    Task<IAsyncDisposable> AcquireAsync(
        string key,
        TimeSpan expiry,
        TimeSpan wait,
        CancellationToken cancellationToken = default);
}
