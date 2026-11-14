using Ticketory.Application.Common;
using Ticketory.Application.Dtos;

namespace Ticketory.Api.Models;

public sealed class AdminMessagesViewModel
{
    public PagedResult<OutboxMessageDto> Messages { get; set; } = null!;
    public IReadOnlyList<string> Types { get; set; } = Array.Empty<string>();
    public string? Type { get; set; }
    public bool? Processed { get; set; }
}
