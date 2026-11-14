using FluentAssertions;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;

namespace Ticketory.Tests.Domain;

public class TicketTypeTests
{
    [Fact]
    public void Reserve_WithinCapacity_IncreasesSold()
    {
        var ticketType = new TicketType { Name = "Standard", Price = 20m, Capacity = 10 };

        ticketType.Reserve(3);

        ticketType.Sold.Should().Be(3);
        ticketType.Available.Should().Be(7);
        ticketType.IsSoldOut.Should().BeFalse();
    }

    [Fact]
    public void Reserve_OverCapacity_Throws()
    {
        var ticketType = new TicketType { Name = "Standard", Price = 20m, Capacity = 2 };
        ticketType.Reserve(2);

        var action = () => ticketType.Reserve(1);

        action.Should().Throw<DomainException>().WithMessage("*Not enough tickets*");
    }

    [Fact]
    public void Release_DoesNotGoBelowZero()
    {
        var ticketType = new TicketType { Name = "Standard", Price = 20m, Capacity = 5 };
        ticketType.Reserve(2);

        ticketType.Release(10);

        ticketType.Sold.Should().Be(0);
    }

    [Fact]
    public void Reserve_WithZeroQuantity_Throws()
    {
        var ticketType = new TicketType { Name = "Standard", Price = 20m, Capacity = 5 };

        var action = () => ticketType.Reserve(0);

        action.Should().Throw<DomainException>();
    }
}
