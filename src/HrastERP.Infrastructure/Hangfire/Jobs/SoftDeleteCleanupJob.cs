using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Hangfire.Jobs;

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
        var cutoff = DateTime.UtcNow.AddDays(-settings.Value.SoftDeleteRetentionDays);
        var utcNow = DateTime.UtcNow;

        var entityTypes = dbContext.Model.GetEntityTypes()
            .Where(et => et.ClrType.IsAssignableTo(typeof(ISoftDeletable)))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "public";

            var pk = entityType.FindPrimaryKey();
            if (pk is null || tableName is null)
                continue;

            var pkColumn = pk.Properties[0].GetColumnName();
            var deletedAtProperty = entityType.FindProperty(nameof(ISoftDeletable.DeletedAt));
            if (deletedAtProperty is null)
                continue;

            var deletedAtColumn = deletedAtProperty.GetColumnName();
            var isTenantEntity = entityType.ClrType.IsAssignableTo(typeof(ITenantEntity));
            var entityName = entityType.ClrType.Name;

            var totalPurged = 0;

            while (true)
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Select candidates with full row JSON for audit
                    var tenantSelect = isTenantEntity
                        ? """t."TenantId"::text"""
                        : $"'{Guid.Empty}'";

                    var selectSql =
                        $$"""
                        SELECT t."{{pkColumn}}"::text AS entity_id,
                               {{tenantSelect}} AS tenant_id,
                               row_to_json(t.*) AS old_values
                        FROM "{{schema}}"."{{tableName}}" t
                        WHERE t."{{deletedAtColumn}}" IS NOT NULL
                          AND t."{{deletedAtColumn}}" < {0}
                        LIMIT {1}
                        """;

                    var candidates = await dbContext.Database
                        .SqlQueryRaw<PurgeCandidateRow>(selectSql, cutoff, BatchSize)
                        .ToListAsync(cancellationToken);

                    if (candidates.Count == 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        break;
                    }

                    var ids = candidates.Select(c => c.entity_id).ToArray();

                    // Insert audit entries
                    foreach (var candidate in candidates)
                    {
                        var auditId = Guid.NewGuid();
                        var tenantId = Guid.TryParse(candidate.tenant_id, out var tid) ? tid : Guid.Empty;

                        await dbContext.Database.ExecuteSqlRawAsync(
                            """
                            INSERT INTO "audit_log" ("Id", "EntityName", "EntityId", "Action", "OldValues", "NewValues", "UserId", "TenantId", "Timestamp")
                            VALUES ({0}, {1}, {2}, 'Purged', {3}, NULL, {4}, {5}, {6})
                            """,
                            [auditId, entityName, candidate.entity_id, candidate.old_values, Guid.Empty, tenantId, utcNow],
                            cancellationToken);
                    }

                    // Delete the batch — table/column names come from EF model metadata, not user input
#pragma warning disable EF1002
                    await dbContext.Database.ExecuteSqlRawAsync(
                        $$"""
                        DELETE FROM "{{schema}}"."{{tableName}}" WHERE "{{pkColumn}}"::text = ANY({0})
                        """,
                        [ids],
                        cancellationToken);
#pragma warning restore EF1002

                    await transaction.CommitAsync(cancellationToken);
                    totalPurged += candidates.Count;

                    if (candidates.Count < BatchSize)
                        break;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            if (totalPurged > 0)
                logger.LogInformation(
                    "Soft-delete cleanup: purged {Count} {EntityName} entities",
                    totalPurged, entityName);
        }
    }

    // Row type for SqlQueryRaw mapping — property names must match SQL column aliases
    private sealed class PurgeCandidateRow
    {
        public string entity_id { get; set; } = string.Empty;
        public string tenant_id { get; set; } = string.Empty;
        public string old_values { get; set; } = string.Empty;
    }
}
