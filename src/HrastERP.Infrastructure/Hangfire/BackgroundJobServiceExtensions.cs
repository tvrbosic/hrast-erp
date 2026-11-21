using Hangfire;
using Hangfire.PostgreSql;
using HrastERP.Infrastructure.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Hangfire;

internal static class BackgroundJobServiceExtensions
{
    /// <summary>
    /// Registers Hangfire settings, PostgreSQL job storage, the Hangfire server and the recurring cleanup jobs.
    /// Called internally by <see cref="InfrastructureServiceExtensions.AddInfrastructure"/>.
    /// The jobs are scheduled later, by <c>RecurringJobRegistrar</c>, once the DI container is built.
    /// </summary>
    internal static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        services
            .AddOptions<HangfireSettings>()
            .BindConfiguration(HangfireSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHangfire((sp, config) =>
        {
            var dbSettings = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            config.UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(dbSettings.ConnectionString),
                new PostgreSqlStorageOptions { SchemaName = DatabaseSchemas.Hangfire });
        });

        services.AddHangfireServer((sp, options) =>
        {
            var hangfireSettings = sp.GetRequiredService<IOptions<HangfireSettings>>().Value;
            options.WorkerCount = hangfireSettings.WorkerCount;
        });

        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();

        // Register cleanup jobs as IRecurringJobDefinition for auto-discovery
        // and by concrete type for Hangfire's job activator
        services.AddScoped<IRecurringJobDefinition, RefreshTokenCleanupJob>();
        services.AddScoped<RefreshTokenCleanupJob>();
        services.AddScoped<IRecurringJobDefinition, SoftDeleteCleanupJob>();
        services.AddScoped<SoftDeleteCleanupJob>();

        return services;
    }

    /// <summary>
    /// Maps the Hangfire dashboard at <see cref="HangfireSettings.DashboardPath"/>, restricted to localhost,
    /// when <see cref="HangfireSettings.DashboardEnabled"/> is true. Does nothing otherwise.
    /// </summary>
    /// <remarks>
    /// Do not enable the dashboard in production unless it is served on an internal port the reverse proxy
    /// does not forward (reached through an SSH tunnel) or protected by real authentication. Behind a reverse
    /// proxy or sidecar on the same host every request arrives from loopback, so the localhost check lets
    /// everyone in.
    /// </remarks>
    internal static WebApplication UseBackgroundJobDashboard(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<HangfireSettings>>().Value;
        if (!settings.DashboardEnabled)
            return app;

        app.UseHangfireDashboard(settings.DashboardPath, new DashboardOptions
        {
            Authorization = [new LocalhostDashboardAuthorizationFilter()]
        });

        return app;
    }
}
