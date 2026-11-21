using Microsoft.AspNetCore.Builder;
using Serilog;

namespace HrastERP.Infrastructure.Logging;

internal static class LoggingServiceExtensions
{
    /// <summary>
    /// Configures Serilog as the logging provider. Minimum levels, enrichers and sinks all come from
    /// the appsettings "Serilog" section; sink and enricher assemblies are found automatically.
    /// Must be called directly on WebApplicationBuilder (not from AddInfrastructure) because
    /// UseSerilog is on builder.Host.
    /// </summary>
    internal static WebApplicationBuilder AddLoggingInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, loggerConfig) =>
            loggerConfig.ReadFrom.Configuration(context.Configuration));

        return builder;
    }

    /// <summary>
    /// Adds Serilog HTTP request logging middleware.
    /// Produces one structured log line per HTTP request with method, path, status code, and elapsed time.
    /// Should be called early in the pipeline, after GlobalExceptionMiddleware.
    /// </summary>
    internal static WebApplication UseLoggingInfrastructure(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        return app;
    }

    /// <summary>
    /// Adds the logging enrichment middleware that pushes UserId and TenantId into Serilog's LogContext.
    /// Must be called after UseAuthentication() so JWT claims are available.
    /// </summary>
    internal static WebApplication UseLoggingEnrichment(this WebApplication app)
    {
        app.UseMiddleware<LoggingEnrichmentMiddleware>();
        return app;
    }
}
