using HrastERP.Infrastructure.Configuration;
using HrastERP.Infrastructure.Persistence;
using HrastERP.Infrastructure.Persistence.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Extensions;

internal static class PersistenceServiceExtensions
{
    /// <summary>
    /// Registers EF Core DbContext, database settings, and persistence interceptors.
    /// Called internally by <see cref="ServiceCollectionExtensions.AddInfrastructure"/>.
    /// </summary>
    internal static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // Bind DatabaseSettings from appsettings.json and validate at startup.
        // ValidateDataAnnotations enforces [Required], [Range], etc. on the settings class.
        // ValidateOnStart ensures misconfiguration fails immediately rather than at first use.
        services
            .AddOptions<DatabaseSettings>()
            .BindConfiguration(DatabaseSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register EF Core interceptors in execution order:
        // 1. AuditableEntityInterceptor — auto-populates CreatedAt/CreatedBy/UpdatedAt/UpdatedBy
        // 2. SoftDeleteInterceptor — converts deletes into soft-delete flag updates
        // 3. TenantEntityInterceptor — auto-populates TenantId on new tenant-scoped entities
        // 4. AuditLogInterceptor — captures entity state changes into audit_log table (MUST be last)
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<TenantEntityInterceptor>();
        services.AddScoped<AuditLogInterceptor>();

        // Register the EF Core DbContext with PostgreSQL and attach all interceptors
        services.AddDbContext<HrastDbContext>((sp, options) =>
        {
            var settings = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            options.UseNpgsql(settings.ConnectionString);
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>(),
                sp.GetRequiredService<TenantEntityInterceptor>(),
                sp.GetRequiredService<AuditLogInterceptor>());
        });

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
