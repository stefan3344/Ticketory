using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;
using Ticketory.Application.Options;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;

namespace Ticketory.Application.Services;

public sealed class VenueService(
    IAppDbContext db,
    ICacheService cache,
    IOptions<CacheOptions> cacheOptions) : IVenueService
{
    public async Task<IReadOnlyList<VenueDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Venues
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new VenueDto(v.Id, v.Name, v.Address, v.City, v.Capacity, v.Events.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<VenueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Venues
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VenueDto(v.Id, v.Name, v.Address, v.City, v.Capacity, v.Events.Count))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<VenueDto> CreateAsync(CreateVenueRequest request, CancellationToken cancellationToken = default)
    {
        var venue = new Venue
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            Capacity = request.Capacity
        };

        db.Venues.Add(venue);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(cancellationToken);
        return new VenueDto(venue.Id, venue.Name, venue.Address, venue.City, venue.Capacity, 0);
    }

    public async Task UpdateAsync(Guid id, UpdateVenueRequest request, CancellationToken cancellationToken = default)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new DomainException("Venue not found.");

        venue.Name = request.Name.Trim();
        venue.Address = request.Address.Trim();
        venue.City = request.City.Trim();
        venue.Capacity = request.Capacity;

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new DomainException("Venue not found.");

        if (await db.Events.AnyAsync(e => e.VenueId == id, cancellationToken))
        {
            throw new DomainException("The venue is used by events and cannot be deleted.");
        }

        db.Venues.Remove(venue);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(cancellationToken);
    }

    private async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        await cache.SetAsync(
            CacheKeys.EventsVersion,
            Guid.NewGuid().ToString("N"),
            TimeSpan.FromDays(cacheOptions.Value.VersionTtlDays),
            cancellationToken);
    }
}
