using HrastERP.API.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HrastERP.API.Extensions;

public static class HealthCheckServiceExtensions
{
    public static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(configuration["Database:ConnectionString"]!, name: "database")
            .AddRedis(configuration["Cache:ConnectionString"]!, name: "redis")
            .AddCheck<SmtpHealthCheck>("smtp");

        return services;
    }

    public static WebApplication UseHealthCheckEndpoint(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        }).AllowAnonymous();

        return app;
    }
}
