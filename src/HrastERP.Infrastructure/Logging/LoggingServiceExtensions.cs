using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace HrastERP.Infrastructure.Logging;

internal static class LoggingServiceExtensions
{
    /// <summary>
    /// Configures Serilog as the logging provider with Console, Debug, and rolling JSON file sinks.
    /// Reads minimum log levels and overrides from appsettings "Serilog" section.
    /// Must be called directly on WebApplicationBuilder (not from AddInfrastructure) because
    /// UseSerilog is on builder.Host.
    /// </summary>
    internal static WebApplicationBuilder AddLoggingInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, loggerConfig) =>
        {
            var filePath = context.Configuration["Serilog:File:Path"] ?? "./logs/hrast-erp-.log";
            var retentionDays = int.TryParse(context.Configuration["Serilog:File:RetentionDays"], out var days)
                ? days
                : 30;

            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console()
                .WriteTo.Debug()
                .WriteTo.File(
                    formatter: new JsonFormatter(),
                    path: filePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retentionDays);
        });

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
