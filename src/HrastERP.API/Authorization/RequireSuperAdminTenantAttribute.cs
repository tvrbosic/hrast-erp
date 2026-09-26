using System.Net;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HrastERP.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSuperAdminTenantAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var currentTenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

        if (currentTenant.TenantId != TenantConstants.SuperAdminTenantId)
        {
            var body = new ErrorResponse(
                "Tenant.SuperAdminRequired",
                "Only super admin tenant users can perform this operation.");

            context.Result = new ObjectResult(body)
            {
                StatusCode = (int)HttpStatusCode.Forbidden
            };
        }
    }
}
