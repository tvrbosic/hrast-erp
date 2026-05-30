using System.Security.Claims;
using HrastERP.SharedKernel.Abstractions;

namespace HrastERP.API.Authentication;

internal sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public Guid TenantId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id"),
            out var id)
            ? id
            : Guid.Empty;
}
