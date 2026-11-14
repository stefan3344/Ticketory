using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ticketory.Application.Abstractions.Email;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Contracts;

namespace Ticketory.Worker.Consumers;

public sealed class OrderCancelledConsumer(
    IEmailSender emailSender,
    ILogger<OrderCancelledConsumer> logger) : IMessageHandler
{
    public string QueueName => "ticketory.order-cancelled";
    public string RoutingKey => RoutingKeys.OrderCancelled;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<OrderCancelledMessage>(payload)
            ?? throw new InvalidOperationException("Could not deserialize OrderCancelledMessage.");

        var rows = new StringBuilder();
        foreach (var item in message.Items)
        {
            rows.Append(
                $"<tr><td>{WebUtility.HtmlEncode(item.TicketTypeName)}</td><td>{item.Quantity}</td></tr>");
        }

        var html = $"""
            <h2>Your Ticketory order was cancelled</h2>
            <p>Your order for <strong>{WebUtility.HtmlEncode(message.EventTitle)}</strong> was cancelled because the event was cancelled.</p>
            <p>No payment was taken. Order reference: <strong>{message.OrderId}</strong></p>
            <table border="1" cellpadding="6" cellspacing="0">
                <thead>
                    <tr><th>Ticket type</th><th>Quantity</th></tr>
                </thead>
                <tbody>{rows}</tbody>
            </table>
            <p>Reason: {WebUtility.HtmlEncode(message.Reason)}</p>
            """;

        await emailSender.SendAsync(
            message.CustomerEmail,
            "Your Ticketory order was cancelled",
            html,
            cancellationToken);

        logger.LogInformation(
            "Sent cancellation email for order {OrderId} to {Email}",
            message.OrderId,
            message.CustomerEmail);
    }
}
