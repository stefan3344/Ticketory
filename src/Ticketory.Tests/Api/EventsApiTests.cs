using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;
using Ticketory.Infrastructure.Persistence;

namespace Ticketory.Tests.Api;

public class EventsApiTests(TicketoryApiFactory factory) : IClassFixture<TicketoryApiFactory>
{
    [Fact]
    public async Task GetEvents_ReturnsSeededEvent()
    {
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EventListItemDto>>(TestJson.Options);
        result.Should().NotBeNull();
        result!.Items.Should().Contain(item => item.Title == "Test Event");
    }

    [Fact]
    public async Task GetEventById_ReturnsTicketTypes()
    {
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var events = await client.GetFromJsonAsync<PagedResult<EventListItemDto>>("/api/events", TestJson.Options);
        var eventId = events!.Items.Single(item => item.Title == "Test Event").Id;

        var details = await client.GetFromJsonAsync<EventDetailsDto>($"/api/events/{eventId}", TestJson.Options);

        details.Should().NotBeNull();
        details!.TicketTypes.Should().Contain(ticketType => ticketType.Name == "Standard");
    }

    [Fact]
    public async Task GetEvents_DoesNotReturnPastEvents()
    {
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var venueId = await db.Venues.Select(venue => venue.Id).FirstAsync();
            var organizerId = await db.Events.Select(ev => ev.OrganizerId).FirstAsync();

            db.Events.Add(new Event
            {
                Title = "Past Event",
                Description = "An event that already started.",
                StartsAt = factory.TimeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
                CreatedAt = factory.TimeProvider.GetUtcNow().UtcDateTime,
                VenueId = venueId,
                OrganizerId = organizerId,
                Status = EventStatus.Published
            });

            await db.SaveChangesAsync();
        }

        var result = await client.GetFromJsonAsync<PagedResult<EventListItemDto>>(
            "/api/events",
            TestJson.Options);

        result.Should().NotBeNull();
        result!.Items.Should().NotContain(item => item.Title == "Past Event");
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
    }
}
