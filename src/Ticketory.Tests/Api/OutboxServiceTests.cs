using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Ticketory.Application.Services;
using Ticketory.Domain.Entities;
using Ticketory.Infrastructure.Persistence;

namespace Ticketory.Tests.Api;

public class OutboxServiceTests
{
    [Fact]
    public async Task GetMessages_SupportsTypeProcessedAndPagingFilters()
    {
        var factory = new TicketoryApiFactory();
        await factory.InitializeAsync();

        try
        {
            await factory.InitializeDatabaseAsync();

            var now = factory.TimeProvider.GetUtcNow().UtcDateTime;
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.OutboxMessages.AddRange(
                new OutboxMessage
                {
                    Type = "OrderCreatedMessage",
                    Payload = "{\"order\":1}",
                    OccurredOn = now,
                    ProcessedOn = now
                },
                new OutboxMessage
                {
                    Type = "OrderRefundedMessage",
                    Payload = "{\"order\":2}",
                    OccurredOn = now.AddMinutes(1)
                });

            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<OutboxService>();

            var all = await service.GetMessagesAsync(null, null, 1, 10);
            all.TotalCount.Should().Be(2);
            all.Items.Should().HaveCount(2);
            all.Items[0].Type.Should().Be("OrderRefundedMessage");
            all.Items[0].Payload.Should().Contain("\n");
            all.Items[0].Payload.Should().Contain("\"order\": 2");

            var byType = await service.GetMessagesAsync("OrderCreatedMessage", null, 1, 10);
            byType.TotalCount.Should().Be(1);
            byType.Items.Single().Type.Should().Be("OrderCreatedMessage");

            var pending = await service.GetMessagesAsync(null, false, 1, 10);
            pending.TotalCount.Should().Be(1);
            pending.Items.Single().Type.Should().Be("OrderRefundedMessage");

            var processed = await service.GetMessagesAsync(null, true, 1, 10);
            processed.TotalCount.Should().Be(1);
            processed.Items.Single().Type.Should().Be("OrderCreatedMessage");

            var types = await service.GetMessageTypesAsync();
            types.Should().Contain("OrderCreatedMessage");
            types.Should().Contain("OrderRefundedMessage");
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }
}
