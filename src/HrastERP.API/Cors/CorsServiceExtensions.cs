using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace HrastERP.API.Cors;

/// <summary>
/// Registers the default CORS policy built from <see cref="CorsSettings"/>. The middleware itself is added
/// with <c>app.UseCors()</c> in <c>Program.cs</c>.
/// </summary>
public static class CorsServiceExtensions
{
    /// <summary>
    /// Binds and validates <see cref="CorsSettings"/> and registers a default policy that allows the
    /// configured origins with any header and method, and exposes <c>Location</c> so browser clients can
    /// read it from 201 responses.
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services)
    {
        // Validate origins on startup: a trailing slash or path never matches the browser's Origin header,
        // which is the most common CORS misconfiguration
        services
            .AddOptions<CorsSettings>()
            .BindConfiguration(CorsSettings.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                settings => settings.AllowedOrigins.All(IsValidOrigin),
                "Each Cors:AllowedOrigins entry must be an absolute http or https origin with no path, query or trailing slash (e.g. https://app.example.com); '*' is not allowed.")
            .ValidateOnStart();

        // Register the CORS services that app.UseCors() needs
        services.AddCors();

        // Build the default policy from the settings. No AllowCredentials(): the API uses bearer tokens, not cookies.
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>((options, settings) =>
            {
                var cors = settings.Value;
                options.AddDefaultPolicy(policy => policy
                    .WithOrigins(cors.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("Location")
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(cors.PreflightMaxAgeMinutes)));
            });

        return services;
    }

    // Valid only if the entry is exactly scheme://host[:port] as a browser sends it in the Origin header.
    // GetLeftPart drops path, query and default ports, and the raw string keeps any whitespace, so comparing
    // the two rejects those; userinfo survives GetLeftPart and is rejected separately.
    private static bool IsValidOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.UserInfo.Length == 0
        && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
}
