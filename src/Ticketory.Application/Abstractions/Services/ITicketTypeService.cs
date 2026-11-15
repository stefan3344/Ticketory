using Ticketory.Application.Dtos;

namespace Ticketory.Application.Abstractions.Services;

public interface ITicketTypeService
{
    Task<IReadOnlyList<TicketTypeDto>> GetForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task<TicketTypeDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TicketTypeDto> CreateAsync(
        Guid actorId,
        bool isAdmin,
        CreateTicketTypeRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        UpdateTicketTypeRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
