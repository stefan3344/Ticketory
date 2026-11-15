using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Abstractions.Identity;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Common;
using Ticketory.Application.Contracts;
using Ticketory.Application.Dtos;
using Ticketory.Application.Options;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Application.Services;

public sealed class OrderService(
    IAppDbContext db,
    ICacheService cache,
    IDistributedLock distributedLock,
    IUserDirectory userDirectory,
    TimeProvider timeProvider,
    IOptions<LockOptions> lockOptions,
    IOptions<CacheOptions> cacheOptions,
    ILogger<OrderService> logger) : IOrderService
{
    private TimeSpan LockExpiry => TimeSpan.FromSeconds(lockOptions.Value.ExpirySeconds);
    private TimeSpan LockWait => TimeSpan.FromSeconds(lockOptions.Value.WaitSeconds);

    public async Task<OrderDto> CreateOrderAsync(
        Guid customerId,
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new DomainException("An order must contain at least one item.");
        }

        var groupedItems = request.Items
            .GroupBy(i => i.TicketTypeId)
            .Select(g => new OrderItemRequest(g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        if (groupedItems.Any(i => i.Quantity <= 0))
        {
            throw new DomainException("Ticket quantity must be greater than zero.");
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await QueryOrders()
                .FirstOrDefaultAsync(
                    o => o.CustomerId == customerId && o.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                logger.LogInformation("Returning existing order {OrderId} for idempotency key", existing.Id);
                return MapOrder(existing);
            }
        }

        var handles = new List<IAsyncDisposable>();
        try
        {
            foreach (var ticketTypeId in groupedItems.Select(i => i.TicketTypeId).OrderBy(id => id))
            {
                handles.Add(await distributedLock.AcquireAsync(
                    $"lock:ticket-type:{ticketTypeId}",
                    LockExpiry,
                    LockWait,
                    cancellationToken));
            }

            var ids = groupedItems.Select(i => i.TicketTypeId);
            var ticketTypes = await db.TicketTypes
                .Include(t => t.Event)
                .Where(t => ids.Contains(t.Id))
                .ToListAsync(cancellationToken);

            if (ticketTypes.Select(t => t.EventId).Distinct().Count() > 1)
            {
                throw new DomainException("An order can only contain tickets for one event.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var order = new Order
            {
                CustomerId = customerId,
                CreatedAt = now,
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
            };

            var messageItems = new List<OrderCreatedItem>();
            foreach (var item in groupedItems)
            {
                var ticketType = ticketTypes.FirstOrDefault(t => t.Id == item.TicketTypeId)
                    ?? throw new DomainException("Ticket type not found.");

                if (ticketType.Event.Status != EventStatus.Published)
                {
                    throw new DomainException($"Event '{ticketType.Event.Title}' is not on sale.");
                }

                if (ticketType.Event.StartsAt <= now)
                {
                    throw new DomainException($"Event '{ticketType.Event.Title}' has already started.");
                }

                ticketType.Reserve(item.Quantity);
                order.AddItem(ticketType, item.Quantity);
                messageItems.Add(new OrderCreatedItem(
                    ticketType.Id,
                    ticketType.Name,
                    ticketType.Event.Title,
                    item.Quantity,
                    ticketType.Price));
            }

            db.Orders.Add(order);

            var email = await userDirectory.GetEmailAsync(customerId, cancellationToken)
                ?? "customer@ticketory.local";
            var message = new OrderCreatedMessage(
                order.Id,
                customerId,
                email,
                order.TotalAmount,
                messageItems);

            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = nameof(OrderCreatedMessage),
                Payload = JsonSerializer.Serialize(message),
                OccurredOn = now
            });

            await db.SaveChangesAsync(cancellationToken);
            await InvalidateEventsAsync(ticketTypes.Select(t => t.EventId), cancellationToken);

            logger.LogInformation(
                "Order {OrderId} created for customer {CustomerId} with total {Total}",
                order.Id,
                customerId,
                order.TotalAmount);

            return MapOrder(order);
        }
        finally
        {
            foreach (var handle in handles)
            {
                await handle.DisposeAsync();
            }
        }
    }

    public async Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var orders = await QueryOrders()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapOrder).ToList();
    }

    public async Task<IReadOnlyList<OrderDto>> GetAllOrdersAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var orders = await QueryOrders()
            .OrderByDescending(o => o.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        return orders.Select(MapOrder).ToList();
    }

    public async Task<OrderDto?> GetOrderAsync(
        Guid id,
        Guid customerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var order = await QueryOrders()
            .FirstOrDefaultAsync(
                o => o.Id == id && (isAdmin || o.CustomerId == customerId),
                cancellationToken);

        return order is null ? null : MapOrder(order);
    }

    public async Task<TicketDto?> GetTicketByCodeAsync(
        string code,
        Guid customerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets
            .AsNoTracking()
            .Include(t => t.TicketType)
            .ThenInclude(t => t.Event)
            .Include(t => t.Order)
            .FirstOrDefaultAsync(t => t.Code == code, cancellationToken);

        if (ticket is null || (!isAdmin && ticket.Order.CustomerId != customerId))
        {
            return null;
        }

        return new TicketDto(
            ticket.Id,
            ticket.Code,
            ticket.TicketType.Name,
            ticket.TicketType.Event.Title,
            ticket.TicketType.Event.StartsAt,
            ticket.Status);
    }

    public async Task<OrderDto> CancelOrderAsync(
        Guid id,
        Guid customerId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.TicketType)
            .ThenInclude(t => t.Event)
            .Include(o => o.Tickets)
            .ThenInclude(t => t.TicketType)
            .ThenInclude(t => t.Event)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new DomainException("Order not found.");

        if (!isAdmin && order.CustomerId != customerId)
        {
            throw new DomainException("You can only cancel your own orders.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return MapOrder(order);
        }

        if (order.Status == OrderStatus.Paid)
        {
            throw new DomainException("Paid orders cannot be cancelled. Please request a refund.");
        }

        foreach (var item in order.Items)
        {
            item.TicketType.Release(item.Quantity);
        }

        order.Cancel(timeProvider.GetUtcNow().UtcDateTime, "Cancelled by customer");
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateEventsAsync(order.Items.Select(i => i.TicketType.EventId), cancellationToken);

        logger.LogInformation("Order {OrderId} cancelled", order.Id);
        return MapOrder(order);
    }

    private IQueryable<Order> QueryOrders()
    {
        return db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.TicketType)
            .ThenInclude(t => t.Event)
            .Include(o => o.Tickets)
            .ThenInclude(t => t.TicketType)
            .ThenInclude(t => t.Event);
    }

    private static OrderDto MapOrder(Order order)
    {
        return new OrderDto(
            order.Id,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,
            order.PaidAt,
            order.CancelledAt,
            order.CancellationReason,
            order.RefundedAt,
            order.RefundReason,
            order.FailureReason,
            order.Items
                .Select(i => new OrderItemDto(
                    i.TicketTypeId,
                    i.TicketType?.Name ?? "Ticket",
                    i.TicketType?.Event?.Title ?? "Event",
                    i.Quantity,
                    i.UnitPrice,
                    i.LineTotal))
                .ToList(),
            order.Tickets
                .Select(t => new TicketDto(
                    t.Id,
                    t.Code,
                    t.TicketType?.Name ?? "Ticket",
                    t.TicketType?.Event?.Title ?? "Event",
                    t.TicketType?.Event?.StartsAt ?? default,
                    t.Status))
                .ToList(),
            order.CustomerId);
    }

    private async Task InvalidateEventsAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken)
    {
        foreach (var eventId in eventIds.Distinct())
        {
            await cache.RemoveAsync(CacheKeys.EventDetails(eventId), cancellationToken);
        }

        await cache.SetAsync(
            CacheKeys.EventsVersion,
            Guid.NewGuid().ToString("N"),
            TimeSpan.FromDays(cacheOptions.Value.VersionTtlDays),
            cancellationToken);
        await cache.RemoveAsync(CacheKeys.Dashboard, cancellationToken);
    }
}
