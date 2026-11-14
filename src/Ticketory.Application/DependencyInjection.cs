using Microsoft.Extensions.DependencyInjection;
using Ticketory.Application.Services;

namespace Ticketory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<EventService>();
        services.AddScoped<VenueService>();
        services.AddScoped<TicketTypeService>();
        services.AddScoped<OrderService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<StatsService>();
        services.AddScoped<OutboxService>();
        return services;
    }
}
