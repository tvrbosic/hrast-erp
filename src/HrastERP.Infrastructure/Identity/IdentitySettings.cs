using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.Identity;

public sealed class IdentitySettings
{
    public const string SectionName = "Identity";

    [Range(6, 128)]
    public int PasswordRequiredLength { get; init; } = 8;

    public bool PasswordRequireDigit { get; init; } = true;

    public bool PasswordRequireLowercase { get; init; } = true;

    public bool PasswordRequireUppercase { get; init; } = true;

    public bool PasswordRequireNonAlphanumeric { get; init; }

    [Range(1, 100)]
    public int LockoutMaxFailedAttempts { get; init; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; init; } = 15;
}
