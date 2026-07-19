using Hangfire;
using Hangfire.PostgreSql;
using HrastERP.Infrastructure.Configuration;
using HrastERP.Infrastructure.Hangfire;
using HrastERP.Infrastructure.Hangfire.Jobs;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Extensions;

internal static class BackgroundJobServiceExtensions
{
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
                options.UseNpgsqlConnection(dbSettings.ConnectionString));
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
}
