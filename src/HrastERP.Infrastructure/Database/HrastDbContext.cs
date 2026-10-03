using System.Linq.Expressions;
using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Database;

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
            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var dbContextRef = Expression.Constant(this, typeof(HrastDbContext));
                var currentTenantIdProp = Expression.Property(dbContextRef, nameof(CurrentTenantId));

                // Super admin tenant bypass: if the current tenant is the super admin tenant,
                // all entities are visible regardless of their TenantId.
                var superAdminConst = Expression.Constant(TenantConstants.SuperAdminTenantId, typeof(Guid));
                var isSuperAdmin = Expression.Equal(currentTenantIdProp, superAdminConst);
                var tenantEquals = Expression.Equal(tenantIdProp, currentTenantIdProp);
                var tenantFilter = Expression.OrElse(isSuperAdmin, tenantEquals);

                filter = filter is null ? tenantFilter : Expression.AndAlso(filter, tenantFilter);
            }

            // If the filter expression is not null, apply it to the entity type.
            if (filter is not null)
            {
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(filter, parameter));
            }
        }

    }
}
