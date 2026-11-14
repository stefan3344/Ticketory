using Ticketory.Domain.Common;
using Ticketory.Domain.Enums;

namespace Ticketory.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public string? RefundReason { get; private set; }
    public string? IdempotencyKey { get; set; }
    public string? FailureReason { get; private set; }

    public ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    public OrderItem AddItem(TicketType ticketType, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        var item = new OrderItem
        {
            TicketTypeId = ticketType.Id,
            TicketType = ticketType,
            Quantity = quantity,
            UnitPrice = ticketType.Price
        };

        Items.Add(item);
        RecalculateTotal();
        return item;
    }

    public void RecalculateTotal() => TotalAmount = Items.Sum(i => i.LineTotal);

    public void MarkPaid(DateTime utcNow)
    {
        if (Status == OrderStatus.Cancelled)
        {
            throw new DomainException("A cancelled order cannot be paid.");
        }

        Status = OrderStatus.Paid;
        PaidAt = utcNow;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        if (Status == OrderStatus.Paid)
        {
            throw new DomainException("A paid order cannot be marked as failed.");
        }

        Status = OrderStatus.Failed;
        FailureReason = reason;
    }

    public void Cancel(DateTime utcNow, string reason)
    {
        if (Status is OrderStatus.Paid or OrderStatus.Refunded)
        {
            throw new DomainException("A paid or refunded order cannot be cancelled.");
        }

        if (Status == OrderStatus.Cancelled)
        {
            return;
        }

        Status = OrderStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = reason;
    }

    public void Refund(DateTime utcNow, string reason)
    {
        if (Status == OrderStatus.Refunded)
        {
            return;
        }

        if (Status != OrderStatus.Paid)
        {
            throw new DomainException("Only paid orders can be refunded.");
        }

        Status = OrderStatus.Refunded;
        RefundedAt = utcNow;
        RefundReason = reason;
    }
}
