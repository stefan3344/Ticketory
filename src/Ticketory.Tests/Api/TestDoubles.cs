using System.Collections.Concurrent;
using Ticketory.Application.Abstractions.Caching;

namespace Ticketory.Tests.Api;

public sealed class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.TryGetValue(key, out var value) ? (T)value : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (value is not null)
        {
            _cache[key] = value;
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        await SetAsync(key, value, timeToLive, cancellationToken);
        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

public sealed class NoopDistributedLock : IDistributedLock
{
    public Task<IAsyncDisposable> AcquireAsync(
        string key,
        TimeSpan expiry,
        TimeSpan wait,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IAsyncDisposable>(new Handle());
    }

    private sealed class Handle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
