using Microsoft.Extensions.DependencyInjection;
using Ticketory.Application.Abstractions.Services;
using Ticketory.Application.Services;

namespace Ticketory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IVenueService, VenueService>();
        services.AddScoped<ITicketTypeService, TicketTypeService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IOutboxService, OutboxService>();
        return services;
    }
}
