using Ticketory.Domain.Enums;

namespace Ticketory.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid TicketTypeId { get; set; }
    public TicketType TicketType { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Issued;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public void Cancel()
    {
        Status = TicketStatus.Cancelled;
    }
}
