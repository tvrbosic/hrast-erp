using System.Linq.Expressions;
using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
    IEnumerable<EntityConfigurationAssembly> moduleAssemblies,
    ICurrentTenant currentTenant) : IdentityUserContext<ApplicationUser, Guid>(options)
{
    // Captured once at construction. Safe because DbContext is scoped — each request gets
    // a new instance with the correct TenantId already resolved from the request context.
    private Guid CurrentTenantId { get; } = currentTenant.TenantId;

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

        ApplyGlobalFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    // Registers soft-delete and tenant isolation query filters for all applicable entity types.
    // Filters are built as expression trees rather than inline lambdas because HasQueryFilter
    // can only be called once per entity type — a second call overwrites the first. Building
    // a single combined predicate per entity avoids that constraint.
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        // Get all entity types EF Core has discovered in the model.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Get the CLR type of the entity.
            var clrType = entityType.ClrType;
            // Create a parameter expression for the entity (e.g. "e" in "WHERE e.DeletedAt IS NULL").
            var parameter = Expression.Parameter(clrType, "e");
            // Initialize a filter expression to null.
            Expression? filter = null;

            // Soft delete filter (check if the entity implements the ISoftDeletable interface)
            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                // Create a property expression for the DeletedAt property.
                var deletedAtProp = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
                // Create a constant expression for null.
                var nullConstant = Expression.Constant(null, typeof(DateTime?));
                // Create a filter expression to check if the DeletedAt property is equal to null.
                filter = Expression.Equal(deletedAtProp, nullConstant);
            }

            // Tenant filter
            if (typeof(ICurrentTenant).IsAssignableFrom(clrType))
            {
                // Create a property expression for the TenantId property.
                var tenantIdProp = Expression.Property(parameter, nameof(ICurrentTenant.TenantId));
                // Create a constant expression for the DbContext instance.
                var dbContextRef = Expression.Constant(this, typeof(HrastDbContext));
                // Create a property expression for the CurrentTenantId property.
                var currentTenantIdProp = Expression.Property(dbContextRef, nameof(CurrentTenantId));
                // Create a filter expression to check if the TenantId property is equal to the CurrentTenantId property.
                var tenantEquals = Expression.Equal(tenantIdProp, currentTenantIdProp);
                // Combine the filter expressions with an AND operator.
                filter = filter is null ? tenantEquals : Expression.AndAlso(filter, tenantEquals);
            }

            // If the filter expression is not null, apply it to the entity type.
            if (filter is not null)
            {
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(filter, parameter));
            }
        }
    }
}
