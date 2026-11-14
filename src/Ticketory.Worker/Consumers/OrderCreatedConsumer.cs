using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Contracts;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Worker.Consumers;

public sealed class OrderCreatedConsumer(
    IAppDbContext db,
    TimeProvider timeProvider,
    ILogger<OrderCreatedConsumer> logger) : IMessageHandler
{
    public string QueueName => "ticketory.order-created";
    public string RoutingKey => RoutingKeys.OrderCreated;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<OrderCreatedMessage>(payload)
            ?? throw new InvalidOperationException("Could not deserialize OrderCreatedMessage.");

        var order = await db.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == message.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogWarning("Order {OrderId} was not found, skipping payment", message.OrderId);
            return;
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.Failed)
        {
            logger.LogInformation("Order {OrderId} is {Status}, skipping payment", order.Id, order.Status);
            return;
        }

        if (order.Status == OrderStatus.Paid || order.Payment is { Status: PaymentStatus.Succeeded })
        {
            logger.LogInformation("Order {OrderId} is already paid, skipping", order.Id);
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(2), timeProvider, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var payment = order.Payment ?? new Payment
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Provider = "Simulated"
        };

        payment.Status = PaymentStatus.Succeeded;
        payment.TransactionReference = $"SIM-{Guid.NewGuid():N}";
        payment.CompletedAt = now;

        if (order.Payment is null)
        {
            db.Payments.Add(payment);
        }

        order.MarkPaid(now);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = nameof(PaymentSucceededMessage),
            Payload = JsonSerializer.Serialize(new PaymentSucceededMessage(
                order.Id,
                payment.Id,
                payment.Amount,
                payment.TransactionReference,
                message.CustomerEmail)),
            OccurredOn = now
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Simulated payment succeeded for order {OrderId}", order.Id);
    }
}
