using Ticketory.Domain.Common;

namespace Ticketory.Domain.Entities;

public class TicketType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int Sold { get; private set; }

    public int Available => Math.Max(0, Capacity - Sold);
    public bool IsSoldOut => Sold >= Capacity;

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        if (Sold + quantity > Capacity)
        {
            throw new DomainException($"Not enough tickets left for '{Name}'. Available: {Available}.");
        }

        Sold += quantity;
    }

    public void Release(int quantity)
    {
        Sold = Math.Max(0, Sold - Math.Max(0, quantity));
    }
}
