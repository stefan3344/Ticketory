using System.ComponentModel.DataAnnotations;
using Ticketory.Domain.Enums;

namespace Ticketory.Application.Dtos;

public sealed record TicketTypeDto(Guid Id, string Name, decimal Price, int Capacity, int Available);

public sealed record EventListItemDto(
    Guid Id,
    string Title,
    DateTime StartsAt,
    string VenueName,
    string City,
    EventStatus Status,
    decimal MinPrice,
    int AvailableTickets,
    double AverageRating,
    int ReviewCount,
    string? ImageUrl);

public sealed record EventDetailsDto(
    Guid Id,
    string Title,
    string Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    Guid VenueId,
    string VenueName,
    string Address,
    string City,
    Guid OrganizerId,
    EventStatus Status,
    double AverageRating,
    IReadOnlyList<TicketTypeDto> TicketTypes,
    IReadOnlyList<ReviewDto> Reviews,
    string? ImageUrl);

public sealed record CreateEventRequest(
    [Required, StringLength(200, MinimumLength = 3)] string Title,
    [Required, StringLength(4000, MinimumLength = 10)] string Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    Guid VenueId,
    [Url, StringLength(500)] string? ImageUrl);

public sealed record UpdateEventRequest(
    [Required, StringLength(200, MinimumLength = 3)] string Title,
    [Required, StringLength(4000, MinimumLength = 10)] string Description,
    DateTime StartsAt,
    DateTime? EndsAt,
    Guid VenueId,
    [Url, StringLength(500)] string? ImageUrl);

public sealed record CreateTicketTypeRequest(
    Guid EventId,
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Range(0, 100000)] decimal Price,
    [Range(1, 100000)] int Capacity);

public sealed record UpdateTicketTypeRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Range(0, 100000)] decimal Price,
    [Range(1, 100000)] int Capacity);
