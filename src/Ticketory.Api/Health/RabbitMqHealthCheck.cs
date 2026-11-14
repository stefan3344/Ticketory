using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ticketory.Infrastructure.Messaging;

namespace Ticketory.Api.Health;

public sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instance = await connection.GetConnectionAsync(cancellationToken);
            return instance.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ check failed.", ex);
        }
    }
}
