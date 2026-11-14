using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Prometheus;
using Serilog;
using Ticketory.Api.Health;
using Ticketory.Api.Options;
using Ticketory.Api.Services;
using Ticketory.Application;
using Ticketory.Infrastructure;
using Ticketory.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, _, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Secret"]))
{
    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration["Jwt:Secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    }
    else
    {
        throw new InvalidOperationException("Jwt:Secret must be configured in non-development environments.");
    }
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<InstanceOptions>(builder.Configuration.GetSection("Instance"));
builder.Services.Configure<ExternalToolsOptions>(builder.Configuration.GetSection("ExternalTools"));
builder.Services.PostConfigure<ExternalToolsOptions>(options =>
{
    options.GrafanaUrl = string.IsNullOrWhiteSpace(options.GrafanaUrl) ? "http://localhost:3000" : options.GrafanaUrl;
    options.DozzleUrl = string.IsNullOrWhiteSpace(options.DozzleUrl) ? "http://localhost:8888" : options.DozzleUrl;
    options.RabbitMqUrl = string.IsNullOrWhiteSpace(options.RabbitMqUrl) ? "http://localhost:15672" : options.RabbitMqUrl;
    options.PrometheusUrl = string.IsNullOrWhiteSpace(options.PrometheusUrl) ? "http://localhost:9090" : options.PrometheusUrl;
    options.MailpitUrl = string.IsNullOrWhiteSpace(options.MailpitUrl) ? "http://localhost:8025" : options.MailpitUrl;
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");

builder.Services
    .AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds)
        };
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "Ticketory.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(DbInitializer.AdminRole));
    options.AddPolicy("OrganizerOrAdmin", policy => policy.RequireRole(DbInitializer.OrganizerRole, DbInitializer.AdminRole));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole(DbInitializer.CustomerRole));
});

builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticketory API",
        Version = "v1",
        Description = "REST API for events, orders, tickets and reviews."
    });

    options.DocInclusionPredicate((_, api) =>
        api.ActionDescriptor.EndpointMetadata.OfType<ApiControllerAttribute>().Any());

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by /api/auth/login."
    });

});

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

var instanceId = app.Services.GetRequiredService<IOptions<InstanceOptions>>().Value.Id;
if (string.IsNullOrWhiteSpace(instanceId))
{
    instanceId = Environment.MachineName;
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Served-By"] = instanceId;
    await next();
});

app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
}

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapMetrics("/metrics");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticketory API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (builder.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

app.Run();

public partial class Program;
