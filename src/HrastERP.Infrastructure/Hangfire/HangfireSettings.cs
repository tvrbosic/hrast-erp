using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.Hangfire;

public sealed class HangfireSettings
{
    public const string SectionName = "Hangfire";

    [Range(1, 20)]
    public int WorkerCount { get; init; } = 1;

    [Range(1, 3650)]
    public int SoftDeleteRetentionDays { get; init; } = 90;

    [Range(1, 365)]
    public int RevokedTokenRetentionDays { get; init; } = 7;

    [Required]
    [MinLength(1)]
    public string SoftDeleteCleanupCron { get; init; } = "0 2 * * *";

    [Required]
    [MinLength(1)]
    public string RefreshTokenCleanupCron { get; init; } = "0 2 * * *";
}
