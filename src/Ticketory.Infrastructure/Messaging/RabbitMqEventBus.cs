using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Infrastructure.Options;

namespace Ticketory.Infrastructure.Messaging;

public sealed class RabbitMqEventBus(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqEventBus> logger) : IEventBus
{
    public async Task PublishAsync(
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var connectionInstance = await connection.GetConnectionAsync(cancellationToken);
        await using var channel = await connectionInstance.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.Value.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(payload);
        await channel.BasicPublishAsync(
            options.Value.Exchange,
            routingKey,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation("Published {RoutingKey} ({Bytes} bytes)", routingKey, body.Length);
    }
}
