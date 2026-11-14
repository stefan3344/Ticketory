using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketory.Application.Abstractions.Email;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Abstractions.Qr;
using Ticketory.Application.Contracts;
using Ticketory.Domain.Enums;

namespace Ticketory.Worker.Consumers;

public sealed class TicketsIssuedConsumer(
    IAppDbContext db,
    IEmailSender emailSender,
    IQrCodeGenerator qrCodeGenerator,
    ILogger<TicketsIssuedConsumer> logger) : IMessageHandler
{
    public string QueueName => "ticketory.tickets-issued";
    public string RoutingKey => RoutingKeys.TicketsIssued;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<TicketsIssuedMessage>(payload)
            ?? throw new InvalidOperationException("Could not deserialize TicketsIssuedMessage.");

        var order = await db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == message.OrderId, cancellationToken);

        if (order is null || order.Status != OrderStatus.Paid)
        {
            logger.LogInformation(
                "Order {OrderId} is {Status}, skipping ticket email",
                message.OrderId,
                order?.Status);
            return;
        }

        var rows = new StringBuilder();
        var inlineImages = new List<EmailInlineImage>();

        foreach (var ticket in message.Tickets)
        {
            var contentId = $"qr-{ticket.TicketId}@ticketory.local";
            var png = qrCodeGenerator.GeneratePng($"TICKETORY:{ticket.Code}");
            inlineImages.Add(new EmailInlineImage(
                contentId,
                $"{ticket.Code}.png",
                "image/png",
                png));

            rows.Append(
                $"<tr><td>{WebUtility.HtmlEncode(ticket.EventTitle)}</td><td>{WebUtility.HtmlEncode(ticket.TicketTypeName)}</td><td>{ticket.StartsAt:yyyy-MM-dd HH:mm} UTC</td><td><code>{WebUtility.HtmlEncode(ticket.Code)}</code></td><td><img src=\"cid:{contentId}\" alt=\"QR code\" width=\"120\" height=\"120\" /></td></tr>");
        }

        var html = $"""
            <h2>Your Ticketory tickets</h2>
            <p>Thank you for your purchase. Order reference: <strong>{message.OrderId}</strong></p>
            <table border="1" cellpadding="6" cellspacing="0">
                <thead>
                    <tr><th>Event</th><th>Ticket type</th><th>Starts</th><th>Code</th><th>QR</th></tr>
                </thead>
                <tbody>{rows}</tbody>
            </table>
            <p>Show the QR code or ticket code at the entrance.</p>
            """;

        await emailSender.SendAsync(
            message.CustomerEmail,
            "Your Ticketory tickets",
            html,
            inlineImages,
            cancellationToken);

        logger.LogInformation(
            "Sent ticket email for order {OrderId} to {Email}",
            message.OrderId,
            message.CustomerEmail);
    }
}
