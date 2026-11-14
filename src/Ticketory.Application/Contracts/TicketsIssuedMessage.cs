namespace Ticketory.Application.Contracts;

public sealed record TicketsIssuedMessage(
    Guid OrderId,
    string CustomerEmail,
    IReadOnlyList<IssuedTicket> Tickets);

public sealed record IssuedTicket(Guid TicketId, string Code, string TicketTypeName, string EventTitle, DateTime StartsAt);
