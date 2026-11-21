using System.Text;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HrastERP.API.Authentication;

/// <summary>
/// Registers JWT access token validation and exposes the validated token's claims to handlers as
/// <see cref="ICurrentUser"/> and <see cref="ICurrentTenant"/>.
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <remarks>
    /// Tokens are issued by <c>TokenService</c> in <c>Infrastructure/Identity</c> with the same
    /// <see cref="JwtSettings"/>, so issuer, audience and signing key must match on both sides.
    /// </remarks>
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Read the JWT settings. The null-forgiving operator is safe: they are only used inside the AddJwtBearer
        // callback, which runs on the first request, and AddIdentityServices() validates them on startup before that
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()!;

        // Register JwtBearer as the default scheme, so [Authorize] uses it without naming it,
        // and configure how incoming tokens are validated
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    // No grace period: a token is rejected the moment it expires (the default allows 5 minutes)
                    ClockSkew = TimeSpan.Zero
                };

                // Write 401s as an ErrorResponse instead of JwtBearer's bodiless default
                options.Events = new JwtBearerEvents { OnChallenge = JwtChallengeResponse.WriteAsync };
            });

        // Register the current user and tenant. Both read the claims of the request's validated token through
        // IHttpContextAccessor, so they are scoped (one per HTTP request)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();

        return services;
    }
}
