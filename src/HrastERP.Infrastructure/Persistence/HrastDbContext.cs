using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Persistence;

/// <summary>
/// The single EF Core DbContext for the entire application.
/// Entity type configurations are not defined here directly — each module registers its assembly
/// as an <see cref="EntityConfigurationAssembly"/> singleton in DI, and <see cref="OnModelCreating"/>
/// scans all registered assemblies for <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/>
/// implementations automatically.
/// </summary>
public sealed class HrastDbContext(
    DbContextOptions<HrastDbContext> options,
    IEnumerable<EntityConfigurationAssembly> moduleAssemblies) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply configurations defined in this assembly, reserved for cross-cutting
        // infrastructure concerns (e.g. outbox table) that do not belong to any specific module
        // (most entities are registered by each module's Infrastructure assembly).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrastDbContext).Assembly);

        // Apply configurations from each module's Infrastructure assembly
        foreach (EntityConfigurationAssembly moduleAssembly in moduleAssemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(moduleAssembly.Assembly);
        }

        base.OnModelCreating(modelBuilder);
    }
}
