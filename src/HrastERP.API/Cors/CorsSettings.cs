using System.ComponentModel.DataAnnotations;

namespace HrastERP.API.Cors;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Origins allowed to call the API from a browser, e.g. <c>https://app.example.com</c>.
    /// Empty means no cross-origin access.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];

    [Range(0, 1440)]
    public int PreflightMaxAgeMinutes { get; init; } = 10;
}
