using Microsoft.EntityFrameworkCore;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;

namespace Ticketory.Application.Services;

public sealed class OutboxService(IAppDbContext db) : IOutboxService
{
    public async Task<PagedResult<OutboxMessageDto>> GetMessagesAsync(
        string? type,
        bool? processed,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var query = db.OutboxMessages
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            query = query.Where(message => message.Type == normalizedType);
        }

        if (processed is not null)
        {
            query = processed.Value
                ? query.Where(message => message.ProcessedOn != null)
                : query.Where(message => message.ProcessedOn == null);
        }

        var total = await query.CountAsync(cancellationToken);
        var rawItems = await query
            .OrderByDescending(message => message.OccurredOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new
            {
                message.Id,
                message.Type,
                message.Payload,
                message.OccurredOn,
                message.ProcessedOn,
                message.Error,
                message.RetryCount
            })
            .ToListAsync(cancellationToken);

        var items = rawItems
            .Select(message => new OutboxMessageDto(
                message.Id,
                message.Type,
                JsonFormatter.PrettyPrint(message.Payload),
                message.OccurredOn,
                message.ProcessedOn,
                message.Error,
                message.RetryCount))
            .ToList();

        return new PagedResult<OutboxMessageDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<string>> GetMessageTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.OutboxMessages
            .AsNoTracking()
            .Select(message => message.Type)
            .Distinct()
            .OrderBy(type => type)
            .ToListAsync(cancellationToken);
    }
}
