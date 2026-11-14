using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Options;
using Ticketory.Domain.Common;

namespace Ticketory.Infrastructure.Caching;

public sealed class RedisDistributedLock(
    IConnectionMultiplexer multiplexer,
    TimeProvider timeProvider,
    IOptions<LockOptions> lockOptions) : IDistributedLock
{
    private const string ReleaseScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    public async Task<IAsyncDisposable> AcquireAsync(
        string key,
        TimeSpan expiry,
        TimeSpan wait,
        CancellationToken cancellationToken = default)
    {
        var database = multiplexer.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var start = timeProvider.GetTimestamp();
        var retryDelay = TimeSpan.FromMilliseconds(lockOptions.Value.RetryDelayMilliseconds);

        while (timeProvider.GetElapsedTime(start) < wait)
        {
            if (await database.StringSetAsync(key, token, expiry, When.NotExists))
            {
                return new LockHandle(database, key, token);
            }

            await Task.Delay(retryDelay, cancellationToken);
        }

        throw new DomainException("Tickets are being reserved by another customer. Please try again.");
    }

    private sealed class LockHandle(IDatabase database, RedisKey key, RedisValue token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await database.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
        }
    }
}
