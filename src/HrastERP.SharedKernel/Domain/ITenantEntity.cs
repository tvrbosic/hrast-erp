namespace HrastERP.SharedKernel.Domain;

/// <summary>
/// Opt-in tenant scoping for an entity (<see cref="BaseEntity{TId}"/> does not implement it).
/// A global query filter limits queries to the current tenant (bypassed for the super admin tenant),
/// and <c>TenantEntityInterceptor</c> fills <see cref="TenantId"/> on insert when it is still empty.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
}
