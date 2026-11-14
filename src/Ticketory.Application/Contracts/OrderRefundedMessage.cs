namespace Ticketory.Application.Contracts;

public sealed record OrderRefundedMessage(
    Guid OrderId,
    string CustomerEmail,
    string EventTitle,
    decimal RefundAmount,
    string RefundReference,
    string Reason,
    IReadOnlyList<string> TicketCodes);
