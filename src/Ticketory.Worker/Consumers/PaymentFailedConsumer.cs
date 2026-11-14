using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Contracts;
using Ticketory.Domain.Enums;

namespace Ticketory.Worker.Consumers;

public sealed class PaymentFailedConsumer(
    IAppDbContext db,
    ILogger<PaymentFailedConsumer> logger) : IMessageHandler
{
    public string QueueName => "ticketory.payment-failed";
    public string RoutingKey => RoutingKeys.PaymentFailed;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<PaymentFailedMessage>(payload)
            ?? throw new InvalidOperationException("Could not deserialize PaymentFailedMessage.");

        var order = await db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.TicketType)
            .FirstOrDefaultAsync(o => o.Id == message.OrderId, cancellationToken);

        if (order is null || order.Status == OrderStatus.Failed)
        {
            return;
        }

        foreach (var item in order.Items)
        {
            item.TicketType.Release(item.Quantity);
        }

        order.MarkFailed(message.Reason);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Order {OrderId} marked as failed: {Reason}", order.Id, message.Reason);
    }
}
