namespace Ticketory.Application.Abstractions.Messaging;

public interface IEventBus
{
    Task PublishAsync(string routingKey, string payload, CancellationToken cancellationToken = default);
}
