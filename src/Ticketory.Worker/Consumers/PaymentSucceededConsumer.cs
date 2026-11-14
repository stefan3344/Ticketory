using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Contracts;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Worker.Consumers;

public sealed class PaymentSucceededConsumer(
    IAppDbContext db,
    TimeProvider timeProvider,
    ILogger<PaymentSucceededConsumer> logger) : IMessageHandler
{
    public string QueueName => "ticketory.payment-succeeded";
    public string RoutingKey => RoutingKeys.PaymentSucceeded;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<PaymentSucceededMessage>(payload)
            ?? throw new InvalidOperationException("Could not deserialize PaymentSucceededMessage.");

        var order = await db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.TicketType)
            .ThenInclude(t => t.Event)
            .Include(o => o.Tickets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == message.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogWarning("Order {OrderId} was not found, skipping ticket issuance", message.OrderId);
            return;
        }

        if (order.Status != OrderStatus.Paid)
        {
            logger.LogInformation("Order {OrderId} is {Status}, skipping ticket issuance", order.Id, order.Status);
            return;
        }

        if (order.Tickets.Count > 0)
        {
            logger.LogInformation("Order {OrderId} already has tickets, skipping", order.Id);
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var issuedTickets = new List<IssuedTicket>();
        foreach (var item in order.Items)
        {
            for (var index = 0; index < item.Quantity; index++)
            {
                var ticket = new Ticket
                {
                    OrderId = order.Id,
                    TicketTypeId = item.TicketTypeId,
                    Code = $"TF-{Guid.NewGuid():N}".ToUpperInvariant(),
                    Status = TicketStatus.Issued,
                    IssuedAt = now
                };

                db.Tickets.Add(ticket);
                issuedTickets.Add(new IssuedTicket(
                    ticket.Id,
                    ticket.Code,
                    item.TicketType.Name,
                    item.TicketType.Event.Title,
                    item.TicketType.Event.StartsAt));
            }
        }

        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = nameof(TicketsIssuedMessage),
            Payload = JsonSerializer.Serialize(new TicketsIssuedMessage(
                order.Id,
                message.CustomerEmail,
                issuedTickets)),
            OccurredOn = now
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Issued {Count} tickets for order {OrderId}", issuedTickets.Count, order.Id);
    }
}
