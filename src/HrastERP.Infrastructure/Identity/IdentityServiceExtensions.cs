using HrastERP.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Identity;

internal static class IdentityServiceExtensions
{
    /// <summary>
    /// Registers ASP.NET Core Identity, JWT settings, and authentication services.
    /// Called internally by <see cref="InfrastructureServiceExtensions.AddInfrastructure"/>.
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

        // Bind the configurable password and lockout policy from the "Identity" section.
        services
            .AddOptions<IdentitySettings>()
            .BindConfiguration(IdentitySettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure ASP.NET Core Identity with the rules that are application invariants, not policy.
        // AddIdentityCore registers UserManager<ApplicationUser> and related services.
        // AddEntityFrameworkStores wires Identity to use HrastDbContext for storing users.
        // AddDefaultTokenProviders registers the providers behind password reset tokens,
        // which the admin ChangePassword command uses; they encrypt tokens with Data Protection.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<HrastDbContext>()
            .AddDefaultTokenProviders();
        services.AddDataProtection();

        // Apply the configured policy on top. AddIdentityCore's lambda has no DI access, so
        // Configure<TDep> resolves IdentitySettings and copies its values into IdentityOptions.
        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<IdentitySettings>>((options, settings) =>
            {
                var identity = settings.Value;
                options.Password.RequiredLength = identity.PasswordRequiredLength;
                options.Password.RequireDigit = identity.PasswordRequireDigit;
                options.Password.RequireLowercase = identity.PasswordRequireLowercase;
                options.Password.RequireUppercase = identity.PasswordRequireUppercase;
                options.Password.RequireNonAlphanumeric = identity.PasswordRequireNonAlphanumeric;

                // Lock an account after this many failed logins in a row (counted by AuthService.LoginAsync).
                options.Lockout.MaxFailedAccessAttempts = identity.LockoutMaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(identity.LockoutMinutes);
            });

        // Register authentication services:
        // - ITokenService creates JWT access tokens and refresh tokens
        // - IAuthService handles login, refresh token rotation, logout and the current user (/me);
        //   it needs ICurrentUser, which the API registers in Program.cs
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
