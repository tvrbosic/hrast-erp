using HrastERP.Infrastructure.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HrastERP.API.HealthChecks;

/// <summary>
/// Registers the dependency health checks (PostgreSQL, Redis, SMTP) and maps them to <c>GET /health</c>.
/// </summary>
public static class HealthCheckServiceExtensions
{
    /// <summary>
    /// Registers the <c>database</c>, <c>redis</c> and <c>smtp</c> checks. The names are the keys
    /// under <c>checks</c> in the <c>/health</c> response.
    /// </summary>
    /// <remarks>
    /// The database check uses the validated <see cref="DatabaseSettings"/> connection string. The redis
    /// check reuses the shared <see cref="IConnectionMultiplexer"/> from the cache services instead of
    /// opening a second connection.
    /// </remarks>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(sp => sp.GetRequiredService<IOptions<DatabaseSettings>>().Value.ConnectionString, name: "database")
            .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis")
            .AddCheck<SmtpHealthCheck>("smtp");

        return services;
    }

    /// <summary>
    /// Maps <c>GET /health</c> without authentication. It returns 200 when the overall status is
    /// Healthy or Degraded and 503 when it is Unhealthy.
    /// </summary>
    public static WebApplication UseHealthCheckEndpoint(this WebApplication app)
    {
        // AllowAnonymous: load balancers and monitoring tools call this endpoint without a JWT
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,

            // Same as the framework defaults; spelled out so the status contract is visible here
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
