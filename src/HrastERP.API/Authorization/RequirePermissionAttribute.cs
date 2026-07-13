using HrastERP.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

// Marks a controller or action as requiring a specific Permission.
// Extends AuthorizeAttribute by encoding the permission as a policy name ("Permission:<long>"),
// which PermissionPolicyProvider later intercepts and translates into an AuthorizationPolicy.
// AllowMultiple = true lets you stack the attribute to require several permissions on one target.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(Permission permission)
    : AuthorizeAttribute($"Permission:{(long)permission}")
{
    public Permission Permission { get; } = permission;
}
