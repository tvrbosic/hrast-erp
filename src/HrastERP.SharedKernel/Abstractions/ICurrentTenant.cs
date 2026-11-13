namespace HrastERP.SharedKernel.Abstractions;

/// <summary>
/// Tenant of the current request, read from the JWT <c>tenant_id</c> claim.
/// </summary>
public interface ICurrentTenant
{
    /// <summary><see cref="Guid.Empty"/> when there is no tenant context (unauthenticated request, startup, background job).</summary>
    Guid TenantId { get; }
}
