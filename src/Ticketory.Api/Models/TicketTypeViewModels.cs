using System.ComponentModel.DataAnnotations;

namespace Ticketory.Api.Models;

public sealed class TicketTypeFormViewModel
{
    public Guid? Id { get; set; }
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100000)]
    public decimal Price { get; set; }

    [Range(1, 100000)]
    public int Capacity { get; set; } = 100;
}
