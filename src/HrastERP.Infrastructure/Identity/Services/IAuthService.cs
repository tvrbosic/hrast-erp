using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Identity;

/// <summary>
/// Login, refresh token rotation, logout and the current user. Login, refresh and logout run without a
/// tenant context, so lookups ignore the tenant and soft-delete query filters and check <c>IsActive</c> /
/// <c>DeletedAt</c> explicitly.
/// </summary>
/// <remarks>
/// Login, refresh and <see cref="GetCurrentUserAsync"/> return the same <see cref="AuthenticatedUser"/>.
/// Its permissions are always the ones in the access token that goes with it.
/// </remarks>
public interface IAuthService
{
    Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// Revokes <paramref name="refreshToken"/> and returns a new token pair. For an inactive or deleted
    /// user it returns <c>Auth.InactiveUser</c> and leaves the token unchanged.
    /// </summary>
    Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes <paramref name="refreshToken"/>. Succeeds even when the token is unknown or already revoked.
    /// </summary>
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Returns the calling user. Permissions come from the caller's access token, so after a role change
    /// they stay the old ones until the next refresh, exactly what the API enforces meanwhile; all other
    /// fields come from the database. Returns <c>Auth.UserNotFound</c> when the user doesn't exist.
    /// </summary>
    Task<Result<AuthenticatedUser>> GetCurrentUserAsync(CancellationToken ct = default);
}
