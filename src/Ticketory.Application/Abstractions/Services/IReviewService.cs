using Ticketory.Application.Dtos;

namespace Ticketory.Application.Abstractions.Services;

public interface IReviewService
{
    Task<IReadOnlyList<ReviewDto>> GetForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task<bool> CanReviewAsync(
        Guid eventId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<ReviewDto> AddAsync(
        Guid eventId,
        Guid customerId,
        string customerName,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default);
}
