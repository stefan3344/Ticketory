using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;
using Ticketory.Infrastructure.Identity;
using Ticketory.Infrastructure.Options;

namespace Ticketory.Infrastructure.Persistence;

public sealed class DbInitializer(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<SeedOptions> seedOptions,
    TimeProvider timeProvider,
    ILogger<DbInitializer> logger)
{
    public const string AdminRole = "Admin";
    public const string OrganizerRole = "Organizer";
    public const string CustomerRole = "Customer";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        foreach (var role in new[] { AdminRole, OrganizerRole, CustomerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var seed = seedOptions.Value;

        await EnsureUserAsync(seed.AdminEmail, seed.AdminPassword, "Ticketory Admin", AdminRole);
        var organizer = await EnsureUserAsync(
            seed.OrganizerEmail,
            seed.OrganizerPassword,
            "Sofia Events Organizer",
            OrganizerRole);
        await EnsureUserAsync(
            seed.CustomerEmail,
            seed.CustomerPassword,
            "Demo Customer",
            CustomerRole);

        if (seed.DemoData && organizer is not null)
        {
            await SeedCatalogAsync(organizer.Id, cancellationToken);
        }
    }

    private async Task<ApplicationUser?> EnsureUserAsync(string email, string password, string fullName, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Skipping seed for role {Role}: email or password is not configured", role);
            return null;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError(
                    "Could not create seed user {Email}: {Errors}",
                    email,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                return null;
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private async Task SeedCatalogAsync(Guid organizerId, CancellationToken cancellationToken)
    {
        if (await db.Events.AnyAsync(cancellationToken))
        {
            return;
        }

        var arena = new Venue
        {
            Name = "Arena Sofia",
            Address = "Bulevard Asen Yordanov 1",
            City = "Sofia",
            Capacity = 12000
        };
        var ndk = new Venue
        {
            Name = "NDK Hall 1",
            Address = "Narodno Sabranie Square 1",
            City = "Sofia",
            Capacity = 3000
        };
        var park = new Venue
        {
            Name = "Borisova Gradina Open Air",
            Address = "Borisova Gradina Park",
            City = "Sofia",
            Capacity = 5000
        };

        db.Venues.AddRange(arena, ndk, park);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        db.Events.AddRange(
            new Event
            {
                Title = "Sofia Rock Night",
                Description = "Three of the biggest Bulgarian rock bands share one stage for a night of loud guitars and sing-alongs.",
                StartsAt = now.Date.AddDays(14).AddHours(19),
                EndsAt = now.Date.AddDays(14).AddHours(23),
                Venue = arena,
                OrganizerId = organizerId,
                Status = EventStatus.Published,
                TicketTypes =
                {
                    new TicketType { Name = "Standard", Price = 45m, Capacity = 4000 },
                    new TicketType { Name = "VIP", Price = 90m, Capacity = 800 },
                    new TicketType { Name = "Backstage", Price = 150m, Capacity = 100 }
                }
            },
            new Event
            {
                Title = "Jazz Under the Stars",
                Description = "An intimate evening with a jazz quartet, featuring standards and original compositions.",
                StartsAt = now.Date.AddDays(7).AddHours(20),
                EndsAt = now.Date.AddDays(7).AddHours(22),
                Venue = ndk,
                OrganizerId = organizerId,
                Status = EventStatus.Published,
                TicketTypes =
                {
                    new TicketType { Name = "Standard", Price = 35m, Capacity = 1200 },
                    new TicketType { Name = "Premium", Price = 60m, Capacity = 300 }
                }
            },
            new Event
            {
                Title = "Stand-up Comedy Marathon",
                Description = "Five comedians, one microphone and two hours of new material in the open air.",
                StartsAt = now.Date.AddDays(21).AddHours(19),
                EndsAt = now.Date.AddDays(21).AddHours(21),
                Venue = park,
                OrganizerId = organizerId,
                Status = EventStatus.Published,
                TicketTypes =
                {
                    new TicketType { Name = "Early Bird", Price = 25m, Capacity = 1000 },
                    new TicketType { Name = "Regular", Price = 35m, Capacity = 2000 }
                }
            });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded demo venues and events");
    }
}
