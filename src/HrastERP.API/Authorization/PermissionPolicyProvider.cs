using HrastERP.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HrastERP.API.Authorization;

// The glue between the attribute and the handler. When ASP.NET Core needs to evaluate a policy
// named "Permission:<long>" (set by RequirePermissionAttribute), this provider intercepts the
// lookup, parses the permission value, and builds an AuthorizationPolicy containing a
// PermissionRequirement. All other policy names are forwarded to the default provider so plain
// [Authorize] (without a policy name) continues to work.
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "Permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix) &&
            long.TryParse(policyName[PolicyPrefix.Length..], out var raw))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement((Permission)raw))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();
}
