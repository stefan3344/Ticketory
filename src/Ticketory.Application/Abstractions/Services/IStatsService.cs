using Ticketory.Application.Dtos;

namespace Ticketory.Application.Abstractions.Services;

public interface IStatsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(
        CancellationToken cancellationToken = default);
}
