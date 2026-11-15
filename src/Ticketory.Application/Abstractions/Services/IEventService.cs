using Ticketory.Application.Common;
using Ticketory.Application.Dtos;

namespace Ticketory.Application.Abstractions.Services;

public interface IEventService
{
    Task<PagedResult<EventListItemDto>> GetPublishedEventsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventListItemDto>> GetManagedEventsAsync(
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<EventDetailsDto?> GetEventDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<EventDetailsDto> CreateEventAsync(
        Guid organizerId,
        CreateEventRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        UpdateEventRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task PublishEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task CancelEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task ReopenEventAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
