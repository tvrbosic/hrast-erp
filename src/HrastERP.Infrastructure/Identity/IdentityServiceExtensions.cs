using HrastERP.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

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

        // Configure ASP.NET Core Identity with password and user rules.
        // AddIdentityCore registers UserManager<ApplicationUser> and related services.
        // AddEntityFrameworkStores wires Identity to use HrastDbContext for storing users.
        // AddDefaultTokenProviders registers the providers behind password reset tokens,
        // which the admin ChangePassword command uses; they encrypt tokens with Data Protection.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;

                // Lock an account for 15 minutes after 5 failed logins in a row (counted by AuthService.LoginAsync).
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<HrastDbContext>()
            .AddDefaultTokenProviders();
        services.AddDataProtection();

        // Register authentication services:
        // - ITokenService creates JWT access tokens and refresh tokens
        // - IAuthService handles login, refresh token rotation, logout and the current user (/me);
        //   it needs ICurrentUser, which the API registers in Program.cs
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
