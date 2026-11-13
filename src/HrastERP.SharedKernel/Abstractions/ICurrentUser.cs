using HrastERP.SharedKernel.Authorization;

namespace HrastERP.SharedKernel.Abstractions;

/// <summary>
/// User of the current request, read from the JWT claims written at login.
/// </summary>
public interface ICurrentUser
{
    /// <summary><see cref="Guid.Empty"/> when the request is not authenticated.</summary>
    Guid UserId { get; }

    /// <summary><see cref="Guid.Empty"/> when the request is not authenticated.</summary>
    Guid TenantId { get; }

    /// <summary>The user's email address.</summary>
    string Username { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// Permissions of the user's role as embedded in the token at login; role changes show up only
    /// after the next token refresh. <see cref="Permission.None"/> when the claim is missing.
    /// </summary>
    Permission EffectivePermissions { get; }
}
