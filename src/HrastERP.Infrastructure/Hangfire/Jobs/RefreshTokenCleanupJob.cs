using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Hangfire.Services;
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
                DELETE FROM refresh_tokens
                WHERE id IN (
                    SELECT id FROM refresh_tokens
                    WHERE expires_at < {0}
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
                DELETE FROM refresh_tokens
                WHERE id IN (
                    SELECT id FROM refresh_tokens
                    WHERE revoked_at IS NOT NULL AND revoked_at < {0}
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
