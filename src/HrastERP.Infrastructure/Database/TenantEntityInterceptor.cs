using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HrastERP.Infrastructure.Database;

/// <summary>
/// Sets <see cref="ITenantEntity.TenantId"/> to the current tenant on insert when it is still
/// <see cref="Guid.Empty"/>, and throws when there is no tenant context to use.
/// </summary>
public sealed class TenantEntityInterceptor(ICurrentTenant currentTenant) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyTenantId(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyTenantId(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void ApplyTenantId(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State != EntityState.Added)
                continue;

            var currentValue = (Guid)entry.Property(nameof(ITenantEntity.TenantId)).CurrentValue!;

            // An explicit value is kept, so the super admin can create entities for other tenants
            if (currentValue != Guid.Empty)
                continue;

            if (currentTenant.TenantId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Cannot save a tenant-scoped entity without a valid tenant context.");
            }

            entry.Property(nameof(ITenantEntity.TenantId)).CurrentValue = currentTenant.TenantId;
        }
    }
}
