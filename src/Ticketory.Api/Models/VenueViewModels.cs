using System.ComponentModel.DataAnnotations;

namespace Ticketory.Api.Models;

public sealed class VenueFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 3)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Range(1, 100000)]
    public int Capacity { get; set; } = 100;
}
