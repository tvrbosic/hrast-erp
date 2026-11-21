using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.Caching;

public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    [Required]
    [MinLength(1)]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int DefaultTtlMinutes { get; init; } = 60;

    /// <summary>
    /// How long, in minutes, <c>BlockInactiveEntityMiddleware</c> caches a user's or tenant's active status.
    /// </summary>
    [Range(1, 60)]
    public int ActiveStatusTtlMinutes { get; init; } = 5;
}
