using System.ComponentModel.DataAnnotations;

namespace Ticketory.Application.Dtos;

public sealed record VenueDto(Guid Id, string Name, string Address, string City, int Capacity, int EventCount);

public sealed record CreateVenueRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [Required, StringLength(250, MinimumLength = 3)] string Address,
    [Required, StringLength(100, MinimumLength = 2)] string City,
    [Range(1, 100000)] int Capacity);

public sealed record UpdateVenueRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [Required, StringLength(250, MinimumLength = 3)] string Address,
    [Required, StringLength(100, MinimumLength = 2)] string City,
    [Range(1, 100000)] int Capacity);
