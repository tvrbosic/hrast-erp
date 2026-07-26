using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.Email;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    [Required]
    [MinLength(1)]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    [Required]
    [MinLength(1)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string FromName { get; init; } = string.Empty;

    public bool UseSsl { get; init; } = true;
}
