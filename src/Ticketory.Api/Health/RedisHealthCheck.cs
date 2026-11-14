using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Ticketory.Api.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis responded in {latency.TotalMilliseconds:F0} ms.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis check failed.", ex);
        }
    }
}
