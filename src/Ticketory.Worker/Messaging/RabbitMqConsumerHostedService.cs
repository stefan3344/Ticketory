using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Infrastructure.Messaging;
using Ticketory.Infrastructure.Options;

namespace Ticketory.Worker.Messaging;

public sealed class RabbitMqConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    ILogger<RabbitMqConsumerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<HandlerMetadata> handlers;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            handlers = scope.ServiceProvider
                .GetServices<IMessageHandler>()
                .Select(handler => new HandlerMetadata(handler.QueueName, handler.RoutingKey))
                .ToList();
        }

        var tasks = handlers
            .Select(metadata => RunConsumerWithRetryAsync(metadata, stoppingToken))
            .ToList();

        await Task.WhenAll(tasks);
    }

    private async Task RunConsumerWithRetryAsync(HandlerMetadata metadata, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerAsync(metadata, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Consumer for queue {Queue} failed; retrying in {Delay} seconds",
                    metadata.QueueName,
                    rabbitMqOptions.Value.ReconnectDelaySeconds);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(rabbitMqOptions.Value.ReconnectDelaySeconds),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task RunConsumerAsync(HandlerMetadata metadata, CancellationToken stoppingToken)
    {
        var connectionInstance = await connection.GetConnectionAsync(stoppingToken);
        var channel = await connectionInstance.CreateChannelAsync(cancellationToken: stoppingToken);
        var gate = new SemaphoreSlim(1, 1);

        try
        {
            await channel.BasicQosAsync(0, (ushort)rabbitMqOptions.Value.PrefetchCount, false, stoppingToken);
            await DeclareTopologyAsync(channel, metadata, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, args) =>
            {
                await gate.WaitAsync(stoppingToken);
                try
                {
                    await ProcessMessageAsync(channel, metadata, args, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error while processing a message from {Queue}", metadata.QueueName);
                }
                finally
                {
                    gate.Release();
                }
            };

            await channel.BasicConsumeAsync(
                metadata.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            logger.LogInformation("Consumer started for queue {Queue}", metadata.QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            gate.Dispose();
            await channel.CloseAsync();
            await channel.DisposeAsync();
        }
    }

    private async Task ProcessMessageAsync(
        IChannel channel,
        HandlerMetadata metadata,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = Encoding.UTF8.GetString(args.Body.Span);

            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetServices<IMessageHandler>()
                .FirstOrDefault(candidate => candidate.QueueName == metadata.QueueName)
                ?? throw new InvalidOperationException($"No handler registered for queue '{metadata.QueueName}'.");

            await handler.HandleAsync(body, cancellationToken);
            await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(channel, metadata, args, ex, cancellationToken);
        }
    }

    private async Task HandleFailureAsync(
        IChannel channel,
        HandlerMetadata metadata,
        BasicDeliverEventArgs args,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var settings = rabbitMqOptions.Value;
        var retryCount = GetRetryCount(args);

        if (exception is JsonException || retryCount >= settings.MaxRetries)
        {
            logger.LogError(
                exception,
                "Moving message from {Queue} to the dead-letter queue",
                metadata.QueueName);

            var deadProperties = new BasicProperties
            {
                Persistent = true,
                Headers = new Dictionary<string, object?>
                {
                    ["x-error"] = exception.Message,
                    ["x-original-queue"] = metadata.QueueName
                }
            };

            await channel.BasicPublishAsync(
                settings.DeadLetterExchange,
                "dead",
                mandatory: false,
                basicProperties: deadProperties,
                body: args.Body,
                cancellationToken: cancellationToken);

            await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
            return;
        }

        logger.LogWarning(
            exception,
            "Retrying message from {Queue} (attempt {Attempt})",
            metadata.QueueName,
            retryCount + 1);

        var retryProperties = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?>
            {
                ["x-retry-count"] = retryCount + 1
            }
        };

        await channel.BasicPublishAsync(
            settings.RetryExchange,
            $"retry.{metadata.QueueName}",
            mandatory: false,
            basicProperties: retryProperties,
            body: args.Body,
            cancellationToken: cancellationToken);

        await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
    }

    private static int GetRetryCount(BasicDeliverEventArgs args)
    {
        if (args.BasicProperties.Headers is not { } headers ||
            !headers.TryGetValue("x-retry-count", out var raw) ||
            raw is null)
        {
            return 0;
        }

        return raw switch
        {
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            int value => value,
            long value => (int)value,
            _ => Convert.ToInt32(raw)
        };
    }

    private async Task DeclareTopologyAsync(
        IChannel channel,
        HandlerMetadata metadata,
        CancellationToken cancellationToken)
    {
        var settings = rabbitMqOptions.Value;

        await channel.ExchangeDeclareAsync(
            settings.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            settings.RetryExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            settings.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            settings.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            settings.DeadLetterQueue,
            settings.DeadLetterExchange,
            "dead",
            arguments: null,
            cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = settings.RetryExchange,
            ["x-dead-letter-routing-key"] = $"retry.{metadata.QueueName}"
        };

        await channel.QueueDeclareAsync(
            metadata.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            metadata.QueueName,
            settings.Exchange,
            metadata.RoutingKey,
            arguments: null,
            cancellationToken: cancellationToken);

        var retryQueueName = $"retry.{metadata.QueueName}";
        var retryArguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = settings.RetryDelayMilliseconds,
            ["x-dead-letter-exchange"] = settings.Exchange,
            ["x-dead-letter-routing-key"] = metadata.RoutingKey
        };

        await channel.QueueDeclareAsync(
            retryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryArguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            retryQueueName,
            settings.RetryExchange,
            $"retry.{metadata.QueueName}",
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private sealed record HandlerMetadata(string QueueName, string RoutingKey);
}
