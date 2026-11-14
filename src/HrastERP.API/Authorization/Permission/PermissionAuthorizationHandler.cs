using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

// Evaluates PermissionRequirement for the current request.
// Registered in DI as IAuthorizationHandler — ASP.NET Core discovers all registered handlers
// and dispatches each requirement to the handler whose generic type parameter matches it
// (AuthorizationHandler<PermissionRequirement> → handles PermissionRequirement).
// Registered as scoped because it depends on scoped ICurrentUser.
public sealed class PermissionAuthorizationHandler(ICurrentUser currentUser)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if ((currentUser.EffectivePermissions & requirement.Permission) != 0)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
