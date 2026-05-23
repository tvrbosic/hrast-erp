using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.Configuration;

public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required]
    [MinLength(1)]
    public string ConnectionString { get; init; } = string.Empty;
}
