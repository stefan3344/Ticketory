using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Common;
using Ticketory.Application.Dtos;
using Ticketory.Application.Options;
using Ticketory.Domain.Enums;

namespace Ticketory.Application.Services;

public sealed class StatsService(
    IAppDbContext db,
    ICacheService cache,
    IOptions<CacheOptions> cacheOptions)
{
    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        return await cache.GetOrSetAsync(
            CacheKeys.Dashboard,
            async token =>
            {
                var revenue = await db.Orders
                    .Where(o => o.Status == OrderStatus.Paid)
                    .SumAsync(o => (decimal?)o.TotalAmount, token) ?? 0m;
                var refundedRevenue = await db.Orders
                    .Where(o => o.Status == OrderStatus.Refunded)
                    .SumAsync(o => (decimal?)o.TotalAmount, token) ?? 0m;

                return new DashboardStatsDto(
                    await db.Events.CountAsync(token),
                    await db.Events.CountAsync(e => e.Status == EventStatus.Published, token),
                    await db.Orders.CountAsync(token),
                    await db.Orders.CountAsync(o => o.Status == OrderStatus.Paid, token),
                    await db.Orders.CountAsync(o => o.Status == OrderStatus.Refunded, token),
                    await db.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled, token),
                    await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, token),
                    await db.Orders.CountAsync(o => o.Status == OrderStatus.Failed, token),
                    revenue,
                    revenue + refundedRevenue,
                    refundedRevenue,
                    await db.Tickets.CountAsync(token),
                    await db.Tickets.CountAsync(
                        t => t.Status == TicketStatus.Issued || t.Status == TicketStatus.CheckedIn,
                        token),
                    await db.Tickets.CountAsync(t => t.Status == TicketStatus.Cancelled, token),
                    await db.OutboxMessages.CountAsync(o => o.ProcessedOn == null, token));
            },
            TimeSpan.FromSeconds(cacheOptions.Value.DashboardTtlSeconds),
            cancellationToken);
    }
}
