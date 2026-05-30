using System.Security.Claims;
using HrastERP.SharedKernel.Abstractions;

namespace HrastERP.API.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    public Guid TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id)
            ? id
            : Guid.Empty;

    public string Username =>
        User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Permissions => [];
}
