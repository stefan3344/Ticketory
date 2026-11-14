using Ticketory.Domain.Common;

namespace Ticketory.Domain.Entities;

public class EventReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "Anonymous";
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static EventReview Create(
        Guid eventId,
        Guid customerId,
        int rating,
        string? comment,
        DateTime createdAt)
    {
        if (rating is < 1 or > 5)
        {
            throw new DomainException("Rating must be between 1 and 5.");
        }

        return new EventReview
        {
            EventId = eventId,
            CustomerId = customerId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = createdAt
        };
    }
}
