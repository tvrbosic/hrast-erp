using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

/// <summary>
/// Registers the API's authorization policies (<see cref="RequireSuperAdminTenantAttribute"/> and
/// <see cref="RequirePermissionAttribute"/>), the handlers that evaluate them, and the 403 response writer.
/// </summary>
public static class AuthorizationServiceExtensions
{
    /// <remarks>
    /// The SuperAdminTenant policy is registered by name; Permission policies are not registered at all but built
    /// on demand by <see cref="PermissionPolicyProvider"/> from the policy name. A failed policy is written as a 403
    /// <c>ErrorResponse</c> by <see cref="ForbiddenResponseHandler"/>.
    /// </remarks>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        // Register the fixed SuperAdminTenant policy. PermissionPolicyProvider resolves it through its fallback
        // to the default provider, which reads these AuthorizationOptions
        services.AddAuthorization(options =>
            options.AddPolicy(RequireSuperAdminTenantAttribute.PolicyName, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new SuperAdminTenantRequirement())));

        // Replace the default policy provider with one that builds "Permission:<long>" policies on demand.
        // Singleton is safe: it keeps no per-request state
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        // Register the handlers that check each requirement. Scoped because they depend on the scoped
        // ICurrentUser / ICurrentTenant (one per HTTP request); a singleton would keep the first request's user
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, SuperAdminTenantAuthorizationHandler>();

        // Write every 403 from a failed policy as an ErrorResponse (the default is a bodiless 403)
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenResponseHandler>();

        return services;
    }
}
