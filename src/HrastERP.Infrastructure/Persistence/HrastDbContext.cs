using System.Linq.Expressions;
using HrastERP.SharedKernel.Domain;
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

        ApplySoftDeleteFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    // Registers an EF Core query filter (WHERE deleted_at IS NULL) for every ISoftDeletable entity.
    // HasQueryFilter requires a typed lambda (e.g. Expression<Func<Order, bool>>), but here the entity
    // types are only known at runtime — so we build the equivalent lambda dynamically via expression
    // trees instead of writing it inline per entity (This saves us from having to write a lambda for each entity).
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var deletedAtProperty = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
            var nullConstant = Expression.Constant(null, typeof(DateTime?));
            var isNull = Expression.Equal(deletedAtProperty, nullConstant);
            var lambda = Expression.Lambda(isNull, parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
