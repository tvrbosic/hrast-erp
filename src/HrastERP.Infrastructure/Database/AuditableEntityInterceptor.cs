using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HrastERP.Infrastructure.Database;

/// <summary>
/// Fills <see cref="IAuditable"/> fields on save: CreatedAt/CreatedBy on insert, UpdatedAt/UpdatedBy on
/// update (UTC; <see cref="Guid.Empty"/> when no user is authenticated). First in the interceptor chain.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentUser currentUser)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditInfo(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditInfo(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void ApplyAuditInfo(DbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var userId = currentUser.IsAuthenticated
            ? currentUser.UserId
            : Guid.Empty;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = utcNow;
                entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = utcNow;
                entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = userId;

                // Never write the creation fields on update, even if the entity was attached with them unset
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
            }
        }
    }
}
