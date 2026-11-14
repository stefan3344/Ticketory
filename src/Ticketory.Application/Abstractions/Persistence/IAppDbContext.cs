using Microsoft.EntityFrameworkCore;
using Ticketory.Domain.Entities;

namespace Ticketory.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    DbSet<Venue> Venues { get; }
    DbSet<Event> Events { get; }
    DbSet<TicketType> TicketTypes { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<EventReview> Reviews { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
