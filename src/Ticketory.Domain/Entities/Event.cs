using Ticketory.Domain.Common;
using Ticketory.Domain.Enums;

namespace Ticketory.Domain.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? ImageUrl { get; set; }
    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;
    public Guid OrganizerId { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
    public ICollection<EventReview> Reviews { get; set; } = new List<EventReview>();

    public bool IsSoldOut => TicketTypes.Count > 0 && TicketTypes.All(t => t.IsSoldOut);

    public double AverageRating => Reviews.Count == 0 ? 0d : Reviews.Average(r => r.Rating);

    public void Publish(DateTime utcNow)
    {
        if (Status == EventStatus.Cancelled)
        {
            throw new DomainException("A cancelled event cannot be published.");
        }

        if (StartsAt <= utcNow)
        {
            throw new DomainException("The event must start in the future.");
        }

        Status = EventStatus.Published;
    }

    public void Cancel()
    {
        if (Status == EventStatus.Completed)
        {
            throw new DomainException("A completed event cannot be cancelled.");
        }

        if (Status == EventStatus.Cancelled)
        {
            return;
        }

        Status = EventStatus.Cancelled;
    }

    public void Reopen(DateTime utcNow)
    {
        if (Status != EventStatus.Cancelled)
        {
            throw new DomainException("Only cancelled events can be reopened.");
        }

        if (StartsAt <= utcNow)
        {
            throw new DomainException("The event must start in the future.");
        }

        Status = EventStatus.Draft;
    }
}
