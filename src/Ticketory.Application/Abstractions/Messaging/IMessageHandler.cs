namespace Ticketory.Application.Abstractions.Messaging;

public interface IMessageHandler
{
    string QueueName { get; }
    string RoutingKey { get; }
    Task HandleAsync(string payload, CancellationToken cancellationToken);
}
