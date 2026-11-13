using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Authentication;

/// <summary>
/// Login, refresh token rotation and logout. Runs without a tenant context, so lookups ignore the
/// tenant and soft-delete query filters and check <c>IsActive</c> / <c>DeletedAt</c> explicitly.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// Revokes <paramref name="refreshToken"/> and returns a new token pair. For an inactive or deleted
    /// user it returns <c>Auth.InactiveUser</c> and leaves the token unchanged.
    /// </summary>
    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes <paramref name="refreshToken"/>. Succeeds even when the token is unknown or already revoked.
    /// </summary>
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);
}
