using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.Database;

public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required]
    [MinLength(1)]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Runs the <c>Seeds/Fixtures</c> scripts at startup. They create a dev admin user with a known
    /// password, so this must never be enabled in production.
    /// </summary>
    public bool SeedFixtures { get; init; }
}
