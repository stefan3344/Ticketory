using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ticketory.Application.Abstractions.Email;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Contracts;

namespace Ticketory.Worker.Consumers;

public sealed class OrderRefundedConsumer(
    IEmailSender emailSender,
    ILogger<OrderRefundedConsumer> logger) : IMessageHandler
{
    public string QueueName => "ticketory.order-refunded";
    public string RoutingKey => RoutingKeys.OrderRefunded;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<OrderRefundedMessage>(payload)
            ?? throw new InvalidOperationException("Could not deserialize OrderRefundedMessage.");

        var ticketCodes = string.Join(
            ", ",
            message.TicketCodes.Select(WebUtility.HtmlEncode));

        var html = $"""
            <h2>Your Ticketory order was refunded</h2>
            <p>Your order for <strong>{WebUtility.HtmlEncode(message.EventTitle)}</strong> was refunded because the event was cancelled.</p>
            <p>Refund amount: <strong>€{message.RefundAmount:0.00}</strong></p>
            <p>Refund reference: <strong>{WebUtility.HtmlEncode(message.RefundReference)}</strong></p>
            <p>Order reference: <strong>{message.OrderId}</strong></p>
            <p>Reason: {WebUtility.HtmlEncode(message.Reason)}</p>
            <p>Ticket codes: {ticketCodes}</p>
            """;

        await emailSender.SendAsync(
            message.CustomerEmail,
            "Your Ticketory order was refunded",
            html,
            cancellationToken);

        logger.LogInformation(
            "Sent refund email for order {OrderId} to {Email}",
            message.OrderId,
            message.CustomerEmail);
    }
}
