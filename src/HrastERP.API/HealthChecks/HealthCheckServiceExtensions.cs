using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
    /// Reads the connection strings from <c>Database:ConnectionString</c> and <c>Cache:ConnectionString</c>.
    /// </remarks>
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
