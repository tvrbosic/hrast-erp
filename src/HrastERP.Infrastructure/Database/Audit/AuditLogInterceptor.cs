using System.Text.Json;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HrastERP.Infrastructure.Database.Audit;

public sealed class AuditLogInterceptor(ICurrentUser currentUser, ICurrentTenant currentTenant)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            AddAuditLogEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            AddAuditLogEntries(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void AddAuditLogEntries(DbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var userId = currentUser.IsAuthenticated ? currentUser.UserId : Guid.Empty;

        // Snapshot entries before adding AuditLogEntry records to the context,
        // which would modify the change tracker during iteration.
        var entries = context.ChangeTracker
            .Entries<IAuditable>()
            .Where(e => e.State is EntityState.Added
                                or EntityState.Modified
                                or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                EntityState.Modified => DetectSoftDelete(entry)
                    ? AuditAction.Deleted
                    : AuditAction.Updated,
                _ => throw new InvalidOperationException(
                    $"Unexpected entity state: {entry.State}")
            };

            var auditLogEntry = new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = GetPrimaryKeyValue(entry),
                Action = action,
                OldValues = BuildOldValues(entry, action),
                NewValues = BuildNewValues(entry, action),
                UserId = userId,
                // Set TenantId explicitly — TenantEntityInterceptor already ran
                // and won't see this newly added entry.
                TenantId = currentTenant.TenantId,
                Timestamp = utcNow
            };

            context.Add(auditLogEntry);
        }
    }

    private static bool DetectSoftDelete(EntityEntry entry)
    {
        return entry.State == EntityState.Modified
            && entry.Properties.Any(p =>
                p.Metadata.Name == nameof(ISoftDeletable.DeletedAt)
                && p.IsModified
                && p.CurrentValue is not null);
    }

    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null)
            return string.Empty;

        var keyValues = primaryKey.Properties
            .Select(p => entry.Property(p.Name).CurrentValue)
            .ToArray();

        return keyValues.Length == 1
            ? keyValues[0]?.ToString() ?? string.Empty
            : JsonSerializer.Serialize(keyValues);
    }

    private static string? BuildOldValues(EntityEntry entry, AuditAction action)
    {
        if (action == AuditAction.Created)
            return null;

        var properties = GetScalarProperties(entry);
        Dictionary<string, object?> values;

        if (action == AuditAction.Deleted)
        {
            // Full snapshot of all properties before deletion
            values = properties
                .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
        }
        else
        {
            // Updated: changed properties only
            values = properties
                .Where(p => p.IsModified)
                .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
        }

        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static string? BuildNewValues(EntityEntry entry, AuditAction action)
    {
        if (action == AuditAction.Deleted)
            return null;

        var properties = GetScalarProperties(entry);
        Dictionary<string, object?> values;

        if (action == AuditAction.Created)
        {
            // Full snapshot of all properties for the new entity
            values = properties
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        }
        else
        {
            // Updated: changed properties only
            values = properties
                .Where(p => p.IsModified)
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        }

        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static IEnumerable<PropertyEntry> GetScalarProperties(EntityEntry entry)
    {
        return entry.Properties
            .Where(p => !p.Metadata.IsShadowProperty());
    }
}
