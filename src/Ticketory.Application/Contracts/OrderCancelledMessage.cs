namespace Ticketory.Application.Contracts;

public sealed record OrderCancelledMessage(
    Guid OrderId,
    string CustomerEmail,
    string EventTitle,
    decimal TotalAmount,
    string Reason,
    IReadOnlyList<OrderCancelledItem> Items);

public sealed record OrderCancelledItem(string TicketTypeName, int Quantity);
