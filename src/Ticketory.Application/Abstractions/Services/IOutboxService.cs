using Ticketory.Application.Common;
using Ticketory.Application.Dtos;

namespace Ticketory.Application.Abstractions.Services;

public interface IOutboxService
{
    Task<PagedResult<OutboxMessageDto>> GetMessagesAsync(
        string? type,
        bool? processed,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetMessageTypesAsync(
        CancellationToken cancellationToken = default);
}
