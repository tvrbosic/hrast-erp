using HrastERP.SharedKernel.Authorization;

namespace HrastERP.SharedKernel.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string Username { get; }
    bool IsAuthenticated { get; }
    Permission EffectivePermissions { get; }
}
