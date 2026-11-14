using System.Text;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HrastERP.API.Authentication;

public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Validates incoming JWT access tokens (JwtBearer as the default scheme) and exposes their claims to
    /// handlers as <see cref="ICurrentUser"/> and <see cref="ICurrentTenant"/>. Tokens are issued by
    /// <c>Infrastructure/Identity</c> (<c>TokenService</c>) with the same <see cref="JwtSettings"/>.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()!;

        // AddAuthentication sets the default scheme so [Authorize] uses it without naming it explicitly.
        // AddJwtBearer configures how incoming tokens are validated (issuer, audience, signing key, expiry).
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
                    ClockSkew = TimeSpan.Zero
                };
            });

        // IHttpContextAccessor gives DI-injected services (like CurrentUser) access to HttpContext,
        // and thus the validated token's claims, outside of controllers and middleware.
        services.AddHttpContextAccessor();

        // Resolved from JWT claims per request by application handlers.
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();

        return services;
    }
}
