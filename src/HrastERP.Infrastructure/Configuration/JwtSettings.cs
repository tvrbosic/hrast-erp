using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required]
    [MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Audience { get; init; } = string.Empty;

    public int AccessTokenExpirationMinutes { get; init; } = 15;

    public int RefreshTokenExpirationDays { get; init; } = 7;
}
