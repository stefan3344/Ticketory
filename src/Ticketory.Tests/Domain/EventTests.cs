using FluentAssertions;
using Ticketory.Domain.Common;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Tests.Domain;

public class EventTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Publish_WithFutureStart_SetsPublished()
    {
        var ev = new Event
        {
            StartsAt = Now.AddHours(1),
            Status = EventStatus.Draft
        };

        ev.Publish(Now);

        ev.Status.Should().Be(EventStatus.Published);
    }

    [Fact]
    public void Publish_WithStartInThePast_Throws()
    {
        var ev = new Event
        {
            StartsAt = Now.AddMinutes(-1),
            Status = EventStatus.Draft
        };

        var action = () => ev.Publish(Now);

        action.Should().Throw<DomainException>().WithMessage("*future*");
    }

    [Fact]
    public void Publish_WithStartExactlyNow_Throws()
    {
        var ev = new Event
        {
            StartsAt = Now,
            Status = EventStatus.Draft
        };

        var action = () => ev.Publish(Now);

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Publish_CancelledEvent_Throws()
    {
        var ev = new Event
        {
            StartsAt = Now.AddHours(1),
            Status = EventStatus.Cancelled
        };

        var action = () => ev.Publish(Now);

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reopen_CancelledFutureEvent_SetsDraft()
    {
        var ev = new Event
        {
            StartsAt = Now.AddHours(1),
            Status = EventStatus.Cancelled
        };

        ev.Reopen(Now);

        ev.Status.Should().Be(EventStatus.Draft);
    }

    [Fact]
    public void Reopen_NonCancelledEvent_Throws()
    {
        var ev = new Event
        {
            StartsAt = Now.AddHours(1),
            Status = EventStatus.Published
        };

        var action = () => ev.Reopen(Now);

        action.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reopen_WithStartInThePast_Throws()
    {
        var ev = new Event
        {
            StartsAt = Now.AddMinutes(-1),
            Status = EventStatus.Cancelled
        };

        var action = () => ev.Reopen(Now);

        action.Should().Throw<DomainException>();
    }
}
