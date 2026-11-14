using FluentAssertions;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;

namespace Ticketory.Tests.Domain;

public class TicketTests
{
    [Fact]
    public void Cancel_SetsCancelledStatus()
    {
        var ticket = new Ticket
        {
            OrderId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Code = "TF-TEST",
            Status = TicketStatus.Issued
        };

        ticket.Cancel();

        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }
}
