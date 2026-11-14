using FluentAssertions;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Tests.Domain;

public class OrderTests
{
    private static TicketType CreateTicketType(decimal price = 20m)
        => new() { Name = "Standard", Price = price, Capacity = 10 };

    [Fact]
    public void AddItem_RecalculatesTotal()
    {
        var order = new Order { CustomerId = Guid.NewGuid() };
        var ticketType = CreateTicketType(35m);

        order.AddItem(ticketType, 2);

        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(70m);
    }

    [Fact]
    public void MarkPaid_SetsStatusAndPaidAt()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var order = new Order { CustomerId = Guid.NewGuid() };
        order.AddItem(CreateTicketType(), 1);

        order.MarkPaid(now);

        order.Status.Should().Be(OrderStatus.Paid);
        order.PaidAt.Should().Be(now);
    }

    [Fact]
    public void CancelPaidOrder_Throws()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var order = new Order { CustomerId = Guid.NewGuid() };
        order.AddItem(CreateTicketType(), 1);
        order.MarkPaid(now);

        var action = () => order.Cancel(now.AddMinutes(1), "test");

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkFailed_SetsReason()
    {
        var order = new Order { CustomerId = Guid.NewGuid() };

        order.MarkFailed("Card declined");

        order.Status.Should().Be(OrderStatus.Failed);
        order.FailureReason.Should().Be("Card declined");
    }

    [Fact]
    public void AddItem_WithInvalidQuantity_Throws()
    {
        var order = new Order { CustomerId = Guid.NewGuid() };

        var action = () => order.AddItem(CreateTicketType(), 0);

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_SetsCancellationAudit()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var order = new Order { CustomerId = Guid.NewGuid() };
        order.AddItem(CreateTicketType(), 1);

        order.Cancel(now, "Event cancelled");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledAt.Should().Be(now);
        order.CancellationReason.Should().Be("Event cancelled");
    }

    [Fact]
    public void Refund_PaidOrder_SetsRefundAudit()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var order = new Order { CustomerId = Guid.NewGuid() };
        order.AddItem(CreateTicketType(), 1);
        order.MarkPaid(now);

        order.Refund(now.AddMinutes(5), "Event cancelled");

        order.Status.Should().Be(OrderStatus.Refunded);
        order.RefundedAt.Should().Be(now.AddMinutes(5));
        order.RefundReason.Should().Be("Event cancelled");
    }

    [Fact]
    public void Refund_PendingOrder_Throws()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var order = new Order { CustomerId = Guid.NewGuid() };

        var action = () => order.Refund(now, "Event cancelled");

        action.Should().Throw<DomainException>();
    }
}
