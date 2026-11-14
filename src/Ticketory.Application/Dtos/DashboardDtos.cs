namespace Ticketory.Application.Dtos;

public sealed record DashboardStatsDto(
    int Events,
    int PublishedEvents,
    int Orders,
    int PaidOrders,
    int RefundedOrders,
    int CancelledOrders,
    int PendingOrders,
    int FailedOrders,
    decimal Revenue,
    decimal GrossRevenue,
    decimal RefundedRevenue,
    int TicketsIssued,
    int TicketsActive,
    int TicketsCancelled,
    int PendingOutbox);
