using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;
using Ticketory.Domain.Enums;
using Ticketory.Infrastructure.Persistence;

namespace Ticketory.Tests.Api;

public class OrdersApiTests(TicketoryApiFactory factory) : IClassFixture<TicketoryApiFactory>
{
    [Fact]
    public async Task RegisterThenCreateOrder_CreatesPendingOrderAndOutboxMessage()
    {
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var email = $"customer-{Guid.NewGuid():N}@test.local";
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Customer12345", "Test Customer"));

        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var events = await client.GetFromJsonAsync<PagedResult<EventListItemDto>>("/api/events", TestJson.Options);
        var eventId = events!.Items.Single(item => item.Title == "Test Event").Id;
        var details = await client.GetFromJsonAsync<EventDetailsDto>($"/api/events/{eventId}", TestJson.Options);
        var ticketTypeId = details!.TicketTypes.Single().Id;

        int soldBefore;
        using (var baselineScope = factory.Services.CreateScope())
        {
            var baselineDb = baselineScope.ServiceProvider.GetRequiredService<AppDbContext>();
            soldBefore = (await baselineDb.TicketTypes.SingleAsync(item => item.Id == ticketTypeId)).Sold;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest(
                new[] { new OrderItemRequest(ticketTypeId, 2) }))
        };
        request.Headers.Add("Idempotency-Key", $"test-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(TestJson.Options);
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(40m);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.OutboxMessages.CountAsync()).Should().BeGreaterThan(0);
        var ticketType = await db.TicketTypes.SingleAsync(item => item.Id == ticketTypeId);
        ticketType.Sold.Should().Be(soldBefore + 2);
    }

    [Fact]
    public async Task CreateOrder_WithSameIdempotencyKey_ReturnsSameOrder()
    {
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var email = $"customer-{Guid.NewGuid():N}@test.local";
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Customer12345", "Test Customer"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var events = await client.GetFromJsonAsync<PagedResult<EventListItemDto>>("/api/events", TestJson.Options);
        var eventId = events!.Items.Single(item => item.Title == "Test Event").Id;
        var details = await client.GetFromJsonAsync<EventDetailsDto>($"/api/events/{eventId}", TestJson.Options);
        var ticketTypeId = details!.TicketTypes.Single().Id;
        var idempotencyKey = $"test-{Guid.NewGuid():N}";

        async Task<OrderDto> CreateAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
            {
                Content = JsonContent.Create(new CreateOrderRequest(
                    new[] { new OrderItemRequest(ticketTypeId, 1) }))
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<OrderDto>(TestJson.Options))!;
        }

        var first = await CreateAsync();
        var second = await CreateAsync();

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task CreateOrder_AfterEventStart_ReturnsBadRequest()
    {
        var timeFactory = new TicketoryApiFactory();
        await timeFactory.InitializeAsync();

        try
        {
            await timeFactory.InitializeDatabaseAsync();
            var client = timeFactory.CreateClient();

            var email = $"customer-{Guid.NewGuid():N}@test.local";
            var registerResponse = await client.PostAsJsonAsync(
                "/api/auth/register",
                new RegisterRequest(email, "Customer12345", "Test Customer"));
            registerResponse.EnsureSuccessStatusCode();

            var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

            var events = await client.GetFromJsonAsync<PagedResult<EventListItemDto>>(
                "/api/events",
                TestJson.Options);
            var eventId = events!.Items.Single(item => item.Title == "Test Event").Id;
            var details = await client.GetFromJsonAsync<EventDetailsDto>(
                $"/api/events/{eventId}",
                TestJson.Options);
            var ticketTypeId = details!.TicketTypes.Single().Id;

            // The seeded event starts 10 days after the fake now; move past it.
            timeFactory.TimeProvider.Advance(TimeSpan.FromDays(11));

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
            {
                Content = JsonContent.Create(new CreateOrderRequest(
                    new[] { new OrderItemRequest(ticketTypeId, 1) }))
            };
            request.Headers.Add("Idempotency-Key", $"test-{Guid.NewGuid():N}");

            var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await ((IAsyncLifetime)timeFactory).DisposeAsync();
        }
    }
}
