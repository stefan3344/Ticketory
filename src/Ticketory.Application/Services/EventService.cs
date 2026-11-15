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

public sealed class EventService(
    IAppDbContext db,
    ICacheService cache,
    IUserDirectory userDirectory,
    TimeProvider timeProvider,
    IOptions<CacheOptions> cacheOptions,
    ILogger<EventService> logger) : IEventService
{
    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    private TimeSpan ListCacheTtl => TimeSpan.FromSeconds(cacheOptions.Value.EventListTtlSeconds);
    private TimeSpan DetailsCacheTtl => TimeSpan.FromSeconds(cacheOptions.Value.EventDetailsTtlSeconds);
    private TimeSpan VersionTtl => TimeSpan.FromDays(cacheOptions.Value.VersionTtlDays);

    public async Task<PagedResult<EventListItemDto>> GetPublishedEventsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var version = await GetVersionAsync(cancellationToken);
        var cacheKey = CacheKeys.PublishedEvents(version, search, page, pageSize);

        return await cache.GetOrSetAsync(
            cacheKey,
            token => QueryPublishedEventsAsync(search, page, pageSize, token),
            ListCacheTtl,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EventListItemDto>> GetManagedEventsAsync(
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var query = db.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .Include(e => e.Reviews)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(e => e.OrganizerId == actorId);
        }

        var events = await query
            .OrderByDescending(e => e.StartsAt)
            .ToListAsync(cancellationToken);

        return events.Select(MapListItem).ToList();
    }

    public async Task<EventDetailsDto?> GetEventDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.EventDetails(id);
        var cached = await cache.GetAsync<EventDetailsDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var entity = await db.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .Include(e => e.Reviews)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var dto = MapDetails(entity);
        await cache.SetAsync(cacheKey, dto, DetailsCacheTtl, cancellationToken);
        return dto;
    }

    public async Task<EventDetailsDto> CreateEventAsync(
        Guid organizerId,
        CreateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.Id == request.VenueId, cancellationToken)
            ?? throw new DomainException("Venue not found.");

        if (request.EndsAt is not null && request.EndsAt <= request.StartsAt)
        {
            throw new DomainException("The end time must be after the start time.");
        }

        var entity = new Event
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            StartsAt = request.StartsAt.ToUniversalTime(),
            EndsAt = request.EndsAt?.ToUniversalTime(),
            VenueId = venue.Id,
            Venue = venue,
            OrganizerId = organizerId,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            Status = EventStatus.Draft,
            CreatedAt = UtcNow
        };

        db.Events.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(entity.Id, cancellationToken);

        logger.LogInformation("Event {EventId} created by organizer {OrganizerId}", entity.Id, organizerId);
        return MapDetails(entity);
    }

    public async Task UpdateEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        UpdateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Events
            .Include(e => e.Venue)
            .Include(e => e.TicketTypes)
            .Include(e => e.Reviews)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException("Event not found.");

        EnsureCanManage(entity, actorId, isAdmin);

        if (request.EndsAt is not null && request.EndsAt <= request.StartsAt)
        {
            throw new DomainException("The end time must be after the start time.");
        }

        var venue = await db.Venues.FirstOrDefaultAsync(v => v.Id == request.VenueId, cancellationToken)
            ?? throw new DomainException("Venue not found.");

        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.StartsAt = request.StartsAt.ToUniversalTime();
        entity.EndsAt = request.EndsAt?.ToUniversalTime();
        entity.VenueId = venue.Id;
        entity.Venue = venue;
        entity.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Events
            .Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException("Event not found.");

        EnsureCanManage(entity, actorId, isAdmin);

        var hasOrders = await db.OrderItems.AnyAsync(i => i.TicketType.EventId == id, cancellationToken);
        if (hasOrders)
        {
            throw new DomainException("The event has orders and cannot be deleted. Cancel it instead.");
        }

        db.Events.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(id, cancellationToken);
    }

    public async Task PublishEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException("Event not found.");

        EnsureCanManage(entity, actorId, isAdmin);
        entity.Publish(UtcNow);

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(id, cancellationToken);
    }

    public async Task CancelEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException("Event not found.");

        EnsureCanManage(entity, actorId, isAdmin);

        if (entity.Status == EventStatus.Cancelled)
        {
            return;
        }

        if (!isAdmin && entity.StartsAt <= UtcNow)
        {
            throw new DomainException("Only admins can cancel an event after it has started.");
        }

        var now = UtcNow;
        var orders = await db.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.TicketType)
            .Include(o => o.Payment)
            .Include(o => o.Tickets)
            .Where(o => o.Items.Any(i => i.TicketType.EventId == id))
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            switch (order.Status)
            {
                case OrderStatus.Pending:
                    foreach (var item in order.Items)
                    {
                        item.TicketType.Release(item.Quantity);
                    }

                    order.Cancel(now, "Event cancelled");
                    db.OutboxMessages.Add(
                        await CreateOrderCancelledOutboxAsync(order, entity.Title, now, cancellationToken));
                    break;

                case OrderStatus.Paid:
                    foreach (var item in order.Items)
                    {
                        item.TicketType.Release(item.Quantity);
                    }

                    order.Refund(now, "Event cancelled");

                    var refundReference = $"REF-{Guid.NewGuid():N}";
                    order.Payment?.Refund(now, refundReference);

                    foreach (var ticket in order.Tickets)
                    {
                        ticket.Cancel();
                    }

                    db.OutboxMessages.Add(
                        await CreateOrderRefundedOutboxAsync(
                            order,
                            entity.Title,
                            refundReference,
                            now,
                            cancellationToken));
                    break;
            }
        }

        entity.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(id, cancellationToken);

        logger.LogInformation(
            "Event {EventId} cancelled; processed {OrderCount} orders",
            id,
            orders.Count);
    }

    public async Task ReopenEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new DomainException("Event not found.");

        EnsureCanManage(entity, actorId, isAdmin);
        entity.Reopen(UtcNow);

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(id, cancellationToken);
    }

    private async Task<OutboxMessage> CreateOrderCancelledOutboxAsync(
        Order order,
        string eventTitle,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var email = await userDirectory.GetEmailAsync(order.CustomerId, cancellationToken)
            ?? "customer@ticketory.local";
        var items = order.Items
            .Select(item => new OrderCancelledItem(item.TicketType.Name, item.Quantity))
            .ToList();
        var message = new OrderCancelledMessage(
            order.Id,
            email,
            eventTitle,
            order.TotalAmount,
            "Event cancelled",
            items);

        return new OutboxMessage
        {
            Type = nameof(OrderCancelledMessage),
            Payload = JsonSerializer.Serialize(message),
            OccurredOn = now
        };
    }

    private async Task<OutboxMessage> CreateOrderRefundedOutboxAsync(
        Order order,
        string eventTitle,
        string refundReference,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var email = await userDirectory.GetEmailAsync(order.CustomerId, cancellationToken)
            ?? "customer@ticketory.local";
        var ticketCodes = order.Tickets.Select(ticket => ticket.Code).ToList();
        var message = new OrderRefundedMessage(
            order.Id,
            email,
            eventTitle,
            order.TotalAmount,
            refundReference,
            "Event cancelled",
            ticketCodes);

        return new OutboxMessage
        {
            Type = nameof(OrderRefundedMessage),
            Payload = JsonSerializer.Serialize(message),
            OccurredOn = now
        };
    }

    private async Task<PagedResult<EventListItemDto>> QueryPublishedEventsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var query = db.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && e.StartsAt > now);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Title.ToLower().Contains(term) ||
                e.Description.ToLower().Contains(term) ||
                e.Venue.City.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.StartsAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventListItemDto(
                e.Id,
                e.Title,
                e.StartsAt,
                e.Venue.Name,
                e.Venue.City,
                e.Status,
                e.TicketTypes.Select(t => (decimal?)t.Price).Min() ?? 0m,
                e.TicketTypes.Select(t => (int?)(t.Capacity - t.Sold)).Sum() ?? 0,
                e.Reviews.Select(r => (double?)r.Rating).Average() ?? 0d,
                e.Reviews.Count(),
                e.ImageUrl))
            .ToListAsync(cancellationToken);

        return new PagedResult<EventListItemDto>(items, page, pageSize, total);
    }

    private static EventListItemDto MapListItem(Event entity) => new(
        entity.Id,
        entity.Title,
        entity.StartsAt,
        entity.Venue?.Name ?? string.Empty,
        entity.Venue?.City ?? string.Empty,
        entity.Status,
        entity.TicketTypes.Count == 0 ? 0m : entity.TicketTypes.Min(t => t.Price),
        entity.TicketTypes.Sum(t => t.Available),
        entity.Reviews.Count == 0 ? 0d : entity.Reviews.Average(r => r.Rating),
        entity.Reviews.Count,
        entity.ImageUrl);

    private static EventDetailsDto MapDetails(Event entity) => new(
        entity.Id,
        entity.Title,
        entity.Description,
        entity.StartsAt,
        entity.EndsAt,
        entity.VenueId,
        entity.Venue?.Name ?? string.Empty,
        entity.Venue?.Address ?? string.Empty,
        entity.Venue?.City ?? string.Empty,
        entity.OrganizerId,
        entity.Status,
        entity.Reviews.Count == 0 ? 0d : entity.Reviews.Average(r => r.Rating),
        entity.TicketTypes
            .OrderBy(t => t.Price)
            .Select(t => new TicketTypeDto(t.Id, t.Name, t.Price, t.Capacity, t.Available))
            .ToList(),
        entity.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.CustomerName, r.Rating, r.Comment, r.CreatedAt))
            .ToList(),
        entity.ImageUrl);

    private static void EnsureCanManage(Event entity, Guid actorId, bool isAdmin)
    {
        if (!isAdmin && entity.OrganizerId != actorId)
        {
            throw new DomainException("You can only manage events that you own.");
        }
    }

    private async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        return await cache.GetOrSetAsync(
            CacheKeys.EventsVersion,
            _ => Task.FromResult("1"),
            VersionTtl,
            cancellationToken);
    }

    private async Task InvalidateAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CacheKeys.EventDetails(eventId), cancellationToken);
        await cache.SetAsync(
            CacheKeys.EventsVersion,
            Guid.NewGuid().ToString("N"),
            VersionTtl,
            cancellationToken);
        await cache.RemoveAsync(CacheKeys.Dashboard, cancellationToken);
    }
}
