using Ticketory.Domain.Common;
using Ticketory.Domain.Enums;

namespace Ticketory.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Provider { get; set; } = "Simulated";
    public string? TransactionReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? RefundReference { get; set; }

    public void Refund(DateTime utcNow, string reference)
    {
        if (Status == PaymentStatus.Refunded)
        {
            return;
        }

        if (Status != PaymentStatus.Succeeded)
        {
            throw new DomainException("Only successful payments can be refunded.");
        }

        Status = PaymentStatus.Refunded;
        RefundedAt = utcNow;
        RefundReference = reference;
    }
}
