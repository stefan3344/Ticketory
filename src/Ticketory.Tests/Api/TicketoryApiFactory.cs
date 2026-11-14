using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;
using Ticketory.Infrastructure.Identity;
using Ticketory.Infrastructure.Persistence;

namespace Ticketory.Tests.Api;

public sealed class TicketoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("ticketory_tests")
        .WithUsername("ticketory")
        .WithPassword("ticketory_tests_password")
        .Build();

    public FakeTimeProvider TimeProvider { get; } =
        new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:InitializeOnStartup", "false");
        builder.UseSetting("Jwt:Issuer", "Ticketory");
        builder.UseSetting("Jwt:Audience", "Ticketory.Clients");
        builder.UseSetting("Jwt:Secret", "test-secret-test-secret-test-secret-1234567890");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IAppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
            services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock, NoopDistributedLock>();
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Admin", "Organizer", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        if (await db.Events.AnyAsync())
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var organizer = new ApplicationUser
        {
            UserName = "organizer@test.local",
            Email = "organizer@test.local",
            FullName = "Test Organizer",
            EmailConfirmed = true
        };

        await userManager.CreateAsync(organizer, "Organizer12345");
        await userManager.AddToRoleAsync(organizer, "Organizer");

        var venue = new Venue
        {
            Name = "Test Venue",
            Address = "Test Address",
            City = "Sofia",
            Capacity = 100
        };

        var eventEntity = new Event
        {
            Title = "Test Event",
            Description = "A test event used by the integration tests.",
            StartsAt = TimeProvider.GetUtcNow().UtcDateTime.AddDays(10),
            CreatedAt = TimeProvider.GetUtcNow().UtcDateTime,
            Venue = venue,
            OrganizerId = organizer.Id,
            Status = EventStatus.Published,
            TicketTypes =
            {
                new TicketType { Name = "Standard", Price = 20m, Capacity = 50 }
            }
        };

        db.Events.Add(eventEntity);
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
