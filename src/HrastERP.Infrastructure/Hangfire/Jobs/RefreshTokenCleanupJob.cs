using HrastERP.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Hangfire.Jobs;

internal sealed class RefreshTokenCleanupJob(
    HrastDbContext dbContext,
    IOptions<HangfireSettings> settings,
    ILogger<RefreshTokenCleanupJob> logger) : IRecurringJobDefinition
{
    private const int BatchSize = 100;

    public string JobId => "infrastructure.refresh-token-cleanup";
    public string CronExpression => settings.Value.RefreshTokenCleanupCron;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow;
        var revokedCutoff = DateTime.UtcNow.AddDays(-settings.Value.RevokedTokenRetentionDays);

        var totalExpired = 0;
        var totalRevoked = 0;

        // Delete expired tokens in batches
        int deleted;
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "RefreshToken"
                WHERE "Id" IN (
                    SELECT "Id" FROM "RefreshToken"
                    WHERE "ExpiresAt" < {0}
                    LIMIT {1}
                )
                """,
                [cutoff, BatchSize],
                cancellationToken);

            totalExpired += deleted;
        } while (deleted == BatchSize);

        // Delete revoked tokens past retention in batches
        do
        {
            deleted = await dbContext.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "RefreshToken"
                WHERE "Id" IN (
                    SELECT "Id" FROM "RefreshToken"
                    WHERE "RevokedAt" IS NOT NULL AND "RevokedAt" < {0}
                    LIMIT {1}
                )
                """,
                [revokedCutoff, BatchSize],
                cancellationToken);

            totalRevoked += deleted;
        } while (deleted == BatchSize);

        logger.LogInformation(
            "Refresh token cleanup completed: {ExpiredCount} expired and {RevokedCount} revoked tokens removed",
            totalExpired, totalRevoked);
    }
}
