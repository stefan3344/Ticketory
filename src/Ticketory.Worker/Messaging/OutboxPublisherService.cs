using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Contracts;
using Ticketory.Application.Options;

namespace Ticketory.Worker.Messaging;

public sealed class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IEventBus eventBus,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> outboxOptions,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher iteration failed");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(outboxOptions.Value.PollIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var settings = outboxOptions.Value;

        var messages = await db.OutboxMessages
            .Where(message => message.ProcessedOn == null && message.RetryCount < settings.MaxRetries)
            .OrderBy(message => message.OccurredOn)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var routingKey = RoutingKeys.ForMessageType(message.Type);
                await eventBus.PublishAsync(routingKey, message.Payload, cancellationToken);

                message.ProcessedOn = timeProvider.GetUtcNow().UtcDateTime;
                message.Error = null;
                logger.LogInformation("Published outbox message {MessageId} ({Type})", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                logger.LogWarning(ex, "Failed to publish outbox message {MessageId}", message.Id);

                if (message.RetryCount >= settings.MaxRetries)
                {
                    message.ProcessedOn = timeProvider.GetUtcNow().UtcDateTime;
                    logger.LogError(
                        "Giving up on outbox message {MessageId} after {Retries} attempts",
                        message.Id,
                        settings.MaxRetries);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
