using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Database.Audit;
using HrastERP.Infrastructure.Hangfire.Services;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Hangfire.Jobs;

/// <summary>
/// Recurring job that permanently deletes soft-deleted rows older than the retention period
/// (<see cref="HangfireSettings.SoftDeleteRetentionDays"/>) and writes a <c>Purged</c> audit entry for each.
/// </summary>
/// <remarks>
/// <list type="number">
///   <item>Computes the cutoff (now minus retention) and one audit timestamp for the whole run.</item>
///   <item>Asks the EF model for every <see cref="ISoftDeletable"/> entity type. Only the list of mapped
///   tables is read, no row data.</item>
///   <item>For each table, resolves the real table, key, <c>deleted_at</c> and <c>tenant_id</c> names from
///   EF metadata and builds the purge SQL with them.</item>
///   <item>Runs that SQL in batches of <see cref="BatchSize"/>. In each batch, Postgres finds the rows past the
///   cutoff, deletes them, and writes one audit entry per deleted row with its full JSON snapshot. Stops when
///   a batch deletes fewer than <see cref="BatchSize"/> rows.</item>
///   <item>Logs how many rows were purged per table.</item>
/// </list>
/// EF only supplies <i>where</i> to look; Postgres decides <i>which rows</i> qualify. Rows are never loaded
/// into C# objects, which keeps the job fast and bypasses the interceptor chain (audit, soft-delete, tenant)
/// that would otherwise turn the delete back into a soft delete.
/// </remarks>
internal sealed class SoftDeleteCleanupJob(
    HrastDbContext dbContext,
    IOptions<HangfireSettings> settings,
    ILogger<SoftDeleteCleanupJob> logger) : IRecurringJobDefinition
{
    private const int BatchSize = 100;

    public string JobId => "infrastructure.soft-delete-cleanup";
    public string CronExpression => settings.Value.SoftDeleteCleanupCron;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Only rows soft-deleted before this point are purged; newer ones stay restorable
        var cutoff = DateTime.UtcNow.AddDays(-settings.Value.SoftDeleteRetentionDays);
        // One timestamp shared by every audit entry written in this run
        var utcNow = DateTime.UtcNow;

        // Every soft-deletable entity in the model (all BaseEntity types), across all modules
        var entityTypes = dbContext.Model.GetEntityTypes()
            .Where(et => et.ClrType.IsAssignableTo(typeof(ISoftDeletable)))
            .ToList();

        // Table/column names come from EF model metadata, not user input
        var audit = dbContext.Model.FindEntityType(typeof(AuditLogEntry))!;
        var auditTable = $"\"{audit.GetSchema() ?? "public"}\".\"{audit.GetTableName()}\"";
        // Column list for the audit INSERT; order must match the SELECT values in the purge SQL
        var auditColumns = string.Join(", ", new[]
        {
            nameof(AuditLogEntry.Id), nameof(AuditLogEntry.EntityName), nameof(AuditLogEntry.EntityId),
            nameof(AuditLogEntry.Action), nameof(AuditLogEntry.OldValues), nameof(AuditLogEntry.NewValues),
            nameof(AuditLogEntry.UserId), nameof(AuditLogEntry.TenantId), nameof(AuditLogEntry.Timestamp)
        }.Select(n => $"\"{audit.FindProperty(n)!.GetColumnName()}\""));

        foreach (var entityType in entityTypes)
        {
            // Resolve the table's real location; entities without a module schema live in "public"
            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "public";

            // Skip types we can't purge by key: keyless types, or types not mapped to a table (e.g. views)
            var pk = entityType.FindPrimaryKey();
            if (pk is null || tableName is null)
                continue;

            // Assumes a single-column key, which BaseEntity<TId> guarantees
            var pkColumn = pk.Properties[0].GetColumnName();
            var deletedAtProperty = entityType.FindProperty(nameof(ISoftDeletable.DeletedAt));
            if (deletedAtProperty is null)
                continue;

            var deletedAtColumn = deletedAtProperty.GetColumnName();

            // Tenant-scoped entities record their own tenant in the audit entry; others get Guid.Empty
            var isTenantEntity = entityType.ClrType.IsAssignableTo(typeof(ITenantEntity));
            // Audit EntityName is the C# class name (e.g. "Tenant"), not the table name
            var entityName = entityType.ClrType.Name;
            var tenantColumn = isTenantEntity
                ? entityType.FindProperty(nameof(ITenantEntity.TenantId))!.GetColumnName()
                : null;

            // Entities without a tenant get Guid.Empty ({5}) as their audit tenant
            var tenantExpression = isTenantEntity ? $"t.\"{tenantColumn}\"" : "{5}";

            // One batch in a single atomic statement:
            //   1. inner SELECT picks up to {1} rows soft-deleted before the cutoff {0}
            //      (no DELETE ... LIMIT in Postgres, so the batch is chosen by subquery)
            //   2. DELETE removes them; RETURNING outputs each removed row's id, tenant and
            //      full JSON snapshot (keys = column names), read just before removal
            //   3. INSERT writes one 'Purged' audit entry per row that DELETE returned, so
            //      rows already removed by a concurrent run get no duplicate entry
            // Affected rows = rows purged; fewer than BatchSize means the table is done.
            var purgeSql =
                $$"""
                WITH purged AS (
                    DELETE FROM "{{schema}}"."{{tableName}}" AS t
                    WHERE t."{{pkColumn}}" IN (
                        SELECT c."{{pkColumn}}"
                        FROM "{{schema}}"."{{tableName}}" AS c
                        WHERE c."{{deletedAtColumn}}" IS NOT NULL
                          AND c."{{deletedAtColumn}}" < {0}
                        LIMIT {1}
                    )
                    RETURNING t."{{pkColumn}}"::text AS entity_id,
                              {{tenantExpression}} AS tenant_id,
                              row_to_json(t.*)::text AS old_values
                )
                INSERT INTO {{auditTable}} ({{auditColumns}})
                SELECT gen_random_uuid(), {2}, entity_id, 'Purged', old_values, NULL, {3}, tenant_id, {4}
                FROM purged
                """;

            var totalPurged = 0;

            while (true)
            {
                // Suppress EF1002 warning about the SQL being generated by the compiler.
                // The SQL is safe and well-formed, and the warning is a false positive.
                #pragma warning disable EF1002
                var purged = await dbContext.Database.ExecuteSqlRawAsync(
                    purgeSql,
                    [cutoff, BatchSize, entityName, Guid.Empty, utcNow, Guid.Empty],
                    cancellationToken);
                #pragma warning restore EF1002

                totalPurged += purged;

                if (purged < BatchSize)
                    break;
            }

            if (totalPurged > 0)
                logger.LogInformation(
                    "Soft-delete cleanup: purged {Count} {EntityName} entities",
                    totalPurged, entityName);
        }
    }
}
