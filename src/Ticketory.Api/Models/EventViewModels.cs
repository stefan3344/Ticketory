using System.ComponentModel.DataAnnotations;
using Ticketory.Application.Dtos;

namespace Ticketory.Api.Models;

public sealed class EventFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [Required, DataType(DataType.DateTime)]
    public DateTime StartsAt { get; set; } = DateTime.Now.AddDays(7);

    [DataType(DataType.DateTime)]
    public DateTime? EndsAt { get; set; }

    [Required]
    public Guid VenueId { get; set; }

    [Url, StringLength(500)]
    public string? ImageUrl { get; set; }

    public IReadOnlyList<VenueDto> Venues { get; set; } = Array.Empty<VenueDto>();
}

public sealed class TicketSelection
{
    public Guid TicketTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Available { get; set; }

    [Range(0, 20)]
    public int Quantity { get; set; }
}

public sealed class BuyTicketsViewModel
{
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    public List<TicketSelection> Items { get; set; } = new();
}

public sealed class EventDetailsPageViewModel
{
    public EventDetailsDto Event { get; set; } = null!;
    public BuyTicketsViewModel Buy { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanCancel { get; set; }
    public bool CanReview { get; set; }
}
