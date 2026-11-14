using HrastERP.API.Responses;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

// Evaluates SuperAdminTenantRequirement. On failure it attaches Tenant.SuperAdminRequired, which
// ForbiddenResponseHandler writes as the 403 body instead of the generic General.Forbidden.
// Scoped because it depends on scoped ICurrentTenant.
public sealed class SuperAdminTenantAuthorizationHandler(ICurrentTenant currentTenant)
    : AuthorizationHandler<SuperAdminTenantRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SuperAdminTenantRequirement requirement)
    {
        if (currentTenant.TenantId == TenantConstants.SuperAdminTenantId)
            context.Succeed(requirement);
        else
            context.Fail(new ErrorFailureReason(this, ApiErrors.SuperAdminTenantRequired));

        return Task.CompletedTask;
    }
}
