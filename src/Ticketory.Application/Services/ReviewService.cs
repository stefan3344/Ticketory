using Microsoft.EntityFrameworkCore;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Application.Services;

public sealed class ReviewService(
    IAppDbContext db,
    ICacheService cache,
    TimeProvider timeProvider) : IReviewService
{
    public async Task<IReadOnlyList<ReviewDto>> GetForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await db.Reviews
            .AsNoTracking()
            .Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.CustomerName, r.Rating, r.Comment, r.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CanReviewAsync(
        Guid eventId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var hasPaidTicket = await db.Tickets.AnyAsync(
            t => t.Order.CustomerId == customerId &&
                 t.Order.Status == OrderStatus.Paid &&
                 t.TicketType.EventId == eventId,
            cancellationToken);

        if (!hasPaidTicket)
        {
            return false;
        }

        return !await db.Reviews.AnyAsync(
            r => r.EventId == eventId && r.CustomerId == customerId,
            cancellationToken);
    }

    public async Task<ReviewDto> AddAsync(
        Guid eventId,
        Guid customerId,
        string customerName,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
            ?? throw new DomainException("Event not found.");

        if (ev.Status is EventStatus.Draft or EventStatus.Cancelled)
        {
            throw new DomainException("You can only review published or completed events.");
        }

        var hasPaidTicket = await db.Tickets.AnyAsync(
            t => t.Order.CustomerId == customerId &&
                 t.Order.Status == OrderStatus.Paid &&
                 t.TicketType.EventId == eventId,
            cancellationToken);

        if (!hasPaidTicket)
        {
            throw new DomainException("Only customers with a paid ticket can review this event.");
        }

        var alreadyReviewed = await db.Reviews.AnyAsync(
            r => r.EventId == eventId && r.CustomerId == customerId,
            cancellationToken);

        if (alreadyReviewed)
        {
            throw new DomainException("You have already reviewed this event.");
        }

        var review = EventReview.Create(
            eventId,
            customerId,
            request.Rating,
            request.Comment,
            timeProvider.GetUtcNow().UtcDateTime);
        review.CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Anonymous" : customerName.Trim();

        db.Reviews.Add(review);
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.EventDetails(eventId), cancellationToken);

        return new ReviewDto(review.Id, review.CustomerName, review.Rating, review.Comment, review.CreatedAt);
    }
}
