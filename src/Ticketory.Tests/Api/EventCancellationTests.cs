using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketory.Application.Common;
using Ticketory.Application.Contracts;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Dtos;
using Ticketory.Domain.Entities;
using Ticketory.Domain.Enums;
using Ticketory.Infrastructure.Persistence;

namespace Ticketory.Tests.Api;

public class EventCancellationTests
{
    [Fact]
    public async Task CancelEvent_RefundsPaidOrdersCancelsPendingOrders_AndCanBeReopened()
    {
        var factory = new TicketoryApiFactory();
        await factory.InitializeAsync();

        try
        {
            await factory.InitializeDatabaseAsync();
            var client = factory.CreateClient();

            var organizerToken = await LoginAsync(client, "organizer@test.local", "Organizer12345");
            var customerToken = await RegisterAsync(client);

            var events = await client.GetFromJsonAsync<PagedResult<EventListItemDto>>(
                "/api/events",
                TestJson.Options);
            var eventId = events!.Items.Single(item => item.Title == "Test Event").Id;
            var details = await client.GetFromJsonAsync<EventDetailsDto>(
                $"/api/events/{eventId}",
                TestJson.Options);
            var ticketTypeId = details!.TicketTypes.Single().Id;

            int soldBefore;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                soldBefore = (await db.TicketTypes.SingleAsync(t => t.Id == ticketTypeId)).Sold;
            }

            var pendingOrder = await CreateOrderAsync(client, customerToken, ticketTypeId, 1);
            var paidOrder = await CreateOrderAsync(client, customerToken, ticketTypeId, 2);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var order = await db.Orders
                    .Include(o => o.Items)
                    .ThenInclude(i => i.TicketType)
                    .Include(o => o.Payment)
                    .Include(o => o.Tickets)
                    .SingleAsync(o => o.Id == paidOrder.Id);

                var now = factory.TimeProvider.GetUtcNow().UtcDateTime;
                order.MarkPaid(now);
                db.Payments.Add(new Payment
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    Status = PaymentStatus.Succeeded,
                    Provider = "Test",
                    TransactionReference = "TEST-REF",
                    CompletedAt = now
                });

                foreach (var item in order.Items)
                {
                    for (var i = 0; i < item.Quantity; i++)
                    {
                        db.Tickets.Add(new Ticket
                        {
                            OrderId = order.Id,
                            TicketTypeId = item.TicketTypeId,
                            Code = $"TF-{Guid.NewGuid():N}".ToUpperInvariant(),
                            Status = TicketStatus.Issued,
                            IssuedAt = now
                        });
                    }
                }

                await db.SaveChangesAsync();
            }

            var cancelResponse = await SendAuthorizedAsync(
                client,
                HttpMethod.Post,
                $"/api/events/{eventId}/cancel",
                organizerToken);
            cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var ev = await db.Events.SingleAsync(e => e.Id == eventId);
                ev.Status.Should().Be(EventStatus.Cancelled);

                var pending = await db.Orders.SingleAsync(o => o.Id == pendingOrder.Id);
                pending.Status.Should().Be(OrderStatus.Cancelled);
                pending.CancelledAt.Should().NotBeNull();
                pending.CancellationReason.Should().Be("Event cancelled");

                var paid = await db.Orders
                    .Include(o => o.Payment)
                    .Include(o => o.Tickets)
                    .SingleAsync(o => o.Id == paidOrder.Id);
                paid.Status.Should().Be(OrderStatus.Refunded);
                paid.RefundedAt.Should().NotBeNull();
                paid.RefundReason.Should().Be("Event cancelled");
                paid.Payment.Should().NotBeNull();
                paid.Payment!.Status.Should().Be(PaymentStatus.Refunded);
                paid.Payment.RefundReference.Should().StartWith("REF-");
                paid.Tickets.Should().OnlyContain(ticket => ticket.Status == TicketStatus.Cancelled);

                var ticketType = await db.TicketTypes.SingleAsync(t => t.Id == ticketTypeId);
                ticketType.Sold.Should().Be(soldBefore);

                (await db.OutboxMessages.CountAsync(m => m.Type == nameof(OrderCancelledMessage)))
                    .Should().BeGreaterThan(0);
                (await db.OutboxMessages.CountAsync(m => m.Type == nameof(OrderRefundedMessage)))
                    .Should().BeGreaterThan(0);
            }

            using (var statsScope = factory.Services.CreateScope())
            {
                var stats = await statsScope.ServiceProvider
                    .GetRequiredService<IStatsService>()
                    .GetDashboardStatsAsync();

                stats.Orders.Should().Be(2);
                stats.PaidOrders.Should().Be(0);
                stats.RefundedOrders.Should().Be(1);
                stats.CancelledOrders.Should().Be(1);
                stats.PendingOrders.Should().Be(0);
                stats.FailedOrders.Should().Be(0);
                stats.Revenue.Should().Be(0m);
                stats.RefundedRevenue.Should().Be(40m);
                stats.GrossRevenue.Should().Be(40m);
                stats.TicketsIssued.Should().Be(2);
                stats.TicketsActive.Should().Be(0);
                stats.TicketsCancelled.Should().Be(2);
            }

            var reopenResponse = await SendAuthorizedAsync(
                client,
                HttpMethod.Post,
                $"/api/events/{eventId}/reopen",
                organizerToken);
            reopenResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ev = await db.Events.SingleAsync(e => e.Id == eventId);
                ev.Status.Should().Be(EventStatus.Draft);
            }

            var publishResponse = await SendAuthorizedAsync(
                client,
                HttpMethod.Post,
                $"/api/events/{eventId}/publish",
                organizerToken);
            publishResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ev = await db.Events.SingleAsync(e => e.Id == eventId);
                ev.Status.Should().Be(EventStatus.Published);
            }
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        return auth!.Token;
    }

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"customer-{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Customer12345", "Test Customer"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        return auth!.Token;
    }

    private static async Task<OrderDto> CreateOrderAsync(
        HttpClient client,
        string token,
        Guid ticketTypeId,
        int quantity)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest(
                new[] { new OrderItemRequest(ticketTypeId, quantity) }))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Idempotency-Key", $"test-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(TestJson.Options))!;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
