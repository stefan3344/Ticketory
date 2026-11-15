using Ticketory.Application.Dtos;

namespace Ticketory.Application.Abstractions.Services;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(
        Guid customerId,
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> GetAllOrdersAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetOrderAsync(
        Guid id,
        Guid customerId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<TicketDto?> GetTicketByCodeAsync(
        string code,
        Guid customerId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<OrderDto> CancelOrderAsync(
        Guid id,
        Guid customerId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
