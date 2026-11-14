using System.ComponentModel.DataAnnotations;
using Ticketory.Domain.Enums;

namespace Ticketory.Application.Dtos;

public sealed record OrderItemRequest(Guid TicketTypeId, [Range(1, 20)] int Quantity);

public sealed record CreateOrderRequest([Required] IReadOnlyList<OrderItemRequest> Items);

public sealed record OrderItemDto(
    Guid TicketTypeId,
    string TicketTypeName,
    string EventTitle,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record TicketDto(
    Guid Id,
    string Code,
    string TicketTypeName,
    string EventTitle,
    DateTime StartsAt,
    TicketStatus Status);

public sealed record OrderDto(
    Guid Id,
    OrderStatus Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    DateTime? RefundedAt,
    string? RefundReason,
    string? FailureReason,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<TicketDto> Tickets,
    Guid CustomerId);
