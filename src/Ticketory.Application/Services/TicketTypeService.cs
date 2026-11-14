using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;
using Ticketory.Application.Options;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;

namespace Ticketory.Application.Services;

public sealed class TicketTypeService(
    IAppDbContext db,
    ICacheService cache,
    IOptions<CacheOptions> cacheOptions)
{
    public async Task<IReadOnlyList<TicketTypeDto>> GetForEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await db.TicketTypes
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.Price)
            .Select(t => new TicketTypeDto(t.Id, t.Name, t.Price, t.Capacity, t.Capacity - t.Sold))
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.TicketTypes
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TicketTypeDto(t.Id, t.Name, t.Price, t.Capacity, t.Capacity - t.Sold))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TicketTypeDto> CreateAsync(
        Guid actorId,
        bool isAdmin,
        CreateTicketTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken)
            ?? throw new DomainException("Event not found.");

        EnsureCanManage(ev, actorId, isAdmin);

        var ticketType = new TicketType
        {
            EventId = ev.Id,
            Name = request.Name.Trim(),
            Price = request.Price,
            Capacity = request.Capacity
        };

        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(ev.Id, cancellationToken);

        return new TicketTypeDto(ticketType.Id, ticketType.Name, ticketType.Price, ticketType.Capacity, ticketType.Available);
    }

    public async Task UpdateAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        UpdateTicketTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticketType = await db.TicketTypes
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new DomainException("Ticket type not found.");

        EnsureCanManage(ticketType.Event, actorId, isAdmin);

        if (request.Capacity < ticketType.Sold)
        {
            throw new DomainException($"Capacity cannot be lower than the {ticketType.Sold} tickets already sold.");
        }

        ticketType.Name = request.Name.Trim();
        ticketType.Price = request.Price;
        ticketType.Capacity = request.Capacity;

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(ticketType.EventId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var ticketType = await db.TicketTypes
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new DomainException("Ticket type not found.");

        EnsureCanManage(ticketType.Event, actorId, isAdmin);

        if (ticketType.Sold > 0)
        {
            throw new DomainException("A ticket type with sold tickets cannot be deleted.");
        }

        if (await db.OrderItems.AnyAsync(i => i.TicketTypeId == id, cancellationToken))
        {
            throw new DomainException("The ticket type is referenced by orders and cannot be deleted.");
        }

        db.TicketTypes.Remove(ticketType);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(ticketType.EventId, cancellationToken);
    }

    private static void EnsureCanManage(Event ev, Guid actorId, bool isAdmin)
    {
        if (!isAdmin && ev.OrganizerId != actorId)
        {
            throw new DomainException("You can only manage ticket types for your own events.");
        }
    }

    private async Task InvalidateAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CacheKeys.EventDetails(eventId), cancellationToken);
        await cache.SetAsync(
            CacheKeys.EventsVersion,
            Guid.NewGuid().ToString("N"),
            TimeSpan.FromDays(cacheOptions.Value.VersionTtlDays),
            cancellationToken);
        await cache.RemoveAsync(CacheKeys.Dashboard, cancellationToken);
    }
}
