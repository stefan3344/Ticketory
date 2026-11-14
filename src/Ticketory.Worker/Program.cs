using Serilog;
using Ticketory.Application;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Infrastructure;
using Ticketory.Worker.Consumers;
using Ticketory.Worker.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger(), dispose: true);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<IMessageHandler, OrderCreatedConsumer>();
builder.Services.AddScoped<IMessageHandler, PaymentSucceededConsumer>();
builder.Services.AddScoped<IMessageHandler, PaymentFailedConsumer>();
builder.Services.AddScoped<IMessageHandler, TicketsIssuedConsumer>();
builder.Services.AddScoped<IMessageHandler, OrderCancelledConsumer>();
builder.Services.AddScoped<IMessageHandler, OrderRefundedConsumer>();

builder.Services.AddHostedService<OutboxPublisherService>();
builder.Services.AddHostedService<RabbitMqConsumerHostedService>();

var host = builder.Build();
await host.RunAsync();
