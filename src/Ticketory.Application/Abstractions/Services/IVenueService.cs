using Ticketory.Application.Dtos;

namespace Ticketory.Application.Abstractions.Services;

public interface IVenueService
{
    Task<IReadOnlyList<VenueDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<VenueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<VenueDto> CreateAsync(
        CreateVenueRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid id,
        UpdateVenueRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
