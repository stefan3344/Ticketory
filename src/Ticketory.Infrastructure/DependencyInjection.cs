using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Ticketory.Application.Abstractions.Caching;
using Ticketory.Application.Abstractions.Email;
using Ticketory.Application.Abstractions.Identity;
using Ticketory.Application.Abstractions.Messaging;
using Ticketory.Application.Abstractions.Persistence;
using Ticketory.Application.Abstractions.Qr;
using Ticketory.Application.Options;
using Ticketory.Infrastructure.Caching;
using Ticketory.Infrastructure.Email;
using Ticketory.Infrastructure.Identity;
using Ticketory.Infrastructure.Messaging;
using Ticketory.Infrastructure.Options;
using Ticketory.Infrastructure.Persistence;
using Ticketory.Infrastructure.Qr;

namespace Ticketory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.Configure<SeedOptions>(configuration.GetSection("Seed"));
        services.Configure<CacheOptions>(configuration.GetSection("Caching"));
        services.Configure<LockOptions>(configuration.GetSection("Locks"));
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        var databaseOptions = configuration.GetSection("Database").Get<DatabaseOptions>()
            ?? new DatabaseOptions();
        var postgresBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(databaseOptions.Password))
        {
            postgresBuilder.Password = databaseOptions.Password;
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresBuilder.ConnectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");
        var redisSettings = configuration.GetSection("Redis").Get<RedisOptions>()
            ?? new RedisOptions();

        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        if (!string.IsNullOrWhiteSpace(redisSettings.Password))
        {
            redisOptions.Password = redisSettings.Password;
        }

        redisOptions.AbortOnConnectFail = redisSettings.AbortOnConnectFail;
        var multiplexer = ConnectionMultiplexer.Connect(redisOptions);

        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisOptions.ToString();
            options.InstanceName = redisSettings.InstanceName;
        });
        services.AddDataProtection()
            .PersistKeysToStackExchangeRedis(multiplexer, redisSettings.DataProtectionKeyName)
            .SetApplicationName("Ticketory");

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IEventBus, RabbitMqEventBus>();

        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<DbInitializer>();

        return services;
    }
}
