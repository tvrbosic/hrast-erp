using System.Linq.Expressions;
using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Domain;
using Microsoft.AspNetCore.Identity;
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSnakeCaseNamingConvention();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's mappings must be applied first so our configurations (e.g. ToTable) win.
        base.OnModelCreating(modelBuilder);

        // Apply configurations for the shared infrastructure entities that belong to no module
        // (users, refresh tokens, roles, stored files, audit log)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrastDbContext).Assembly);

        // Apply configurations from each module's assembly
        foreach (EntityConfigurationAssembly moduleAssembly in moduleAssemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(moduleAssembly.Assembly);
        }

        // Identity sets these table names explicitly, so the naming convention does not rewrite them.
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");

        ApplyGlobalFilters(modelBuilder);
    }

    // Registers soft-delete and tenant isolation query filters for all applicable entity types.
    // Filters are built as expression trees rather than inline lambdas because HasQueryFilter
    // can only be called once per entity type — a second call overwrites the first. Building
    // a single combined predicate per entity avoids that constraint.
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // The lambda parameter "e" in the filter e => ...
            var clrType = entityType.ClrType;
            var parameter = Expression.Parameter(clrType, "e");
            Expression? filter = null;

            // Soft-deletable entities: e.DeletedAt == null
            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                var deletedAtProp = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
                var nullConstant = Expression.Constant(null, typeof(DateTime?));
                filter = Expression.Equal(deletedAtProp, nullConstant);
            }

            // Tenant entities: e.TenantId == CurrentTenantId, ANDed with the soft-delete check
            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                // Referencing the context instance (not a constant Guid) makes EF read CurrentTenantId
                // from the context running each query, so the cached model works for every tenant
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

            // Entities with neither interface get no filter
            if (filter is not null)
            {
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(filter, parameter));
            }
        }

    }
}
