using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;
using Ticketory.Infrastructure.Identity;

namespace Ticketory.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<EventReview> Reviews => Set<EventReview>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        });

        builder.Entity<Venue>(entity =>
        {
            entity.ToTable("Venues");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).HasMaxLength(150).IsRequired();
            entity.Property(v => v.Address).HasMaxLength(250).IsRequired();
            entity.Property(v => v.City).HasMaxLength(100).IsRequired();
            entity.HasIndex(v => v.City);
        });

        builder.Entity<Event>(entity =>
        {
            entity.ToTable("Events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(e => new { e.Status, e.StartsAt });
            entity.HasIndex(e => e.OrganizerId);

            entity.HasOne(e => e.Venue)
                .WithMany(v => v.Events)
                .HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TicketType>(entity =>
        {
            entity.ToTable("TicketTypes");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Price).HasPrecision(18, 2);
            entity.HasIndex(t => t.EventId);

            entity.HasOne(t => t.Event)
                .WithMany(e => e.TicketTypes)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(o => o.IdempotencyKey).HasMaxLength(100);
            entity.Property(o => o.FailureReason).HasMaxLength(500);
            entity.HasIndex(o => new { o.CustomerId, o.IdempotencyKey }).IsUnique();
            entity.HasIndex(o => o.CreatedAt);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.HasIndex(i => new { i.OrderId, i.TicketTypeId }).IsUnique();

            entity.HasOne(i => i.TicketType)
                .WithMany()
                .HasForeignKey(i => i.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(p => p.Provider).HasMaxLength(50).IsRequired();
            entity.Property(p => p.TransactionReference).HasMaxLength(100);
            entity.HasIndex(p => p.OrderId).IsUnique();

            entity.HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Code).HasMaxLength(64).IsRequired();
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(t => t.Code).IsUnique();
            entity.HasIndex(t => t.OrderId);

            entity.HasOne(t => t.TicketType)
                .WithMany()
                .HasForeignKey(t => t.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventReview>(entity =>
        {
            entity.ToTable("EventReviews");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.CustomerName).HasMaxLength(150).IsRequired();
            entity.Property(r => r.Comment).HasMaxLength(2000);
            entity.HasIndex(r => new { r.EventId, r.CustomerId }).IsUnique();

            entity.HasOne(r => r.Event)
                .WithMany(e => e.Reviews)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).HasMaxLength(200).IsRequired();
            entity.Property(o => o.Payload).IsRequired();
            entity.Property(o => o.Error).HasMaxLength(2000);
            entity.HasIndex(o => o.ProcessedOn);
            entity.HasIndex(o => o.OccurredOn);
        });

    }
}
