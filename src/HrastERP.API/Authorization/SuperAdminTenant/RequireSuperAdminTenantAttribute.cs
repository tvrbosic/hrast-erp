using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

// Restricts a controller or action to users of the super admin tenant.
// Names the "SuperAdminTenant" policy (registered in Program.cs), which SuperAdminTenantAuthorizationHandler
// evaluates. Can be stacked with [RequirePermission]; both must pass.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSuperAdminTenantAttribute() : AuthorizeAttribute(PolicyName)
{
    public const string PolicyName = "SuperAdminTenant";
}
