using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Configuration;
using HrastERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Extensions;

internal static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Registers ASP.NET Core Identity, JWT settings, and authentication services.
    /// Called internally by <see cref="ServiceCollectionExtensions.AddInfrastructure"/>.
    /// </summary>
    internal static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        // Bind JwtSettings from appsettings.json and validate at startup.
        // Makes IOptions<JwtSettings> injectable (used by TokenService to generate tokens).
        services
            .AddOptions<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure ASP.NET Core Identity with password and user rules.
        // AddIdentityCore registers UserManager<ApplicationUser> and related services.
        // AddEntityFrameworkStores wires Identity to use HrastDbContext for storing users.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<HrastDbContext>();

        // Register authentication services:
        // - ITokenService generates and validates JWT + refresh tokens
        // - IAuthService handles login/register flows using Identity and ITokenService
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}