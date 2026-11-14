namespace Ticketory.Application.Dtos;

public sealed record OutboxMessageDto(
    Guid Id,
    string Type,
    string Payload,
    DateTime OccurredOn,
    DateTime? ProcessedOn,
    string? Error,
    int RetryCount);
