namespace Ticketory.Application.Contracts;

public sealed record OrderCreatedMessage(
    Guid OrderId,
    Guid CustomerId,
    string CustomerEmail,
    decimal TotalAmount,
    IReadOnlyList<OrderCreatedItem> Items);

public sealed record OrderCreatedItem(Guid TicketTypeId, string TicketTypeName, string EventTitle, int Quantity, decimal UnitPrice);
