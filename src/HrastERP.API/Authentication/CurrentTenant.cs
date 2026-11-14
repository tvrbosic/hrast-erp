using System.Security.Claims;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Abstractions;

namespace HrastERP.API.Authentication;

internal sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public Guid TenantId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirstValue(JwtClaimNames.TenantId),
            out var id)
            ? id
            : Guid.Empty;
}
