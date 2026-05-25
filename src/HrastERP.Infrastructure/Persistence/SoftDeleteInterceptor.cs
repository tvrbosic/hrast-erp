using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HrastERP.Infrastructure.Persistence;

public sealed class SoftDeleteInterceptor(ICurrentUser currentUser)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            ApplySoftDelete(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            ApplySoftDelete(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void ApplySoftDelete(DbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var userId = currentUser.IsAuthenticated ? currentUser.UserId : Guid.Empty;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = utcNow;
            entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = userId;
        }
    }
}
