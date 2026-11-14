using FluentAssertions;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Tests.Domain;

public class PaymentTests
{
    [Fact]
    public void Refund_SucceededPayment_SetsRefundAudit()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var payment = new Payment
        {
            OrderId = Guid.NewGuid(),
            Amount = 20m,
            Status = PaymentStatus.Succeeded
        };

        payment.Refund(now, "REF-123");

        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAt.Should().Be(now);
        payment.RefundReference.Should().Be("REF-123");
    }

    [Fact]
    public void Refund_PendingPayment_Throws()
    {
        var payment = new Payment
        {
            OrderId = Guid.NewGuid(),
            Amount = 20m,
            Status = PaymentStatus.Pending
        };

        var action = () => payment.Refund(DateTime.UtcNow, "REF-123");

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Refund_AlreadyRefunded_IsIdempotent()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var payment = new Payment
        {
            OrderId = Guid.NewGuid(),
            Amount = 20m,
            Status = PaymentStatus.Succeeded
        };
        payment.Refund(now, "REF-123");

        payment.Refund(now.AddMinutes(1), "REF-456");

        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundReference.Should().Be("REF-123");
    }
}
