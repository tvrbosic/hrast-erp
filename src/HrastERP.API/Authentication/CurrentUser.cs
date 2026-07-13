using System.Security.Claims;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;

namespace HrastERP.API.Authentication;

// Resolves the current user from the active HTTP request's JWT claims.
// Registered as ICurrentUser in DI and injected into application handlers.
// Properties are lazily evaluated — each access reads directly from HttpContext.User,
// which is populated by the JWT Bearer middleware after validating the Authorization header.
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    // ClaimsPrincipal set by the JWT middleware; null outside of an HTTP request context.
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    // Parsed from the standard NameIdentifier claim (sub) written by TokenService.
    // Falls back to Guid.Empty for unauthenticated requests (e.g. audit interceptor fallback).
    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    // Parsed from the custom "tenant_id" claim written by TokenService.
    // Falls back to Guid.Empty when the claim is absent or the request is unauthenticated.
    public Guid TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id)
            ? id
            : Guid.Empty;

    // Email address from the standard Email claim written by TokenService.
    public string Username =>
        User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    // Parsed from the custom "permissions" claim written by TokenService.
    // The claim stores the Permission flags enum as a long integer.
    // Falls back to Permission.None when the claim is absent or malformed.
    public Permission EffectivePermissions =>
        long.TryParse(User?.FindFirstValue("permissions"), out var raw)
            ? (Permission)raw
            : Permission.None;
}
