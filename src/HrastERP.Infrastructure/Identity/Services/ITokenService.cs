namespace HrastERP.Infrastructure.Identity;

/// <summary>
/// Creates the JWT access token and the refresh tokens used by <see cref="IAuthService"/>.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Expects <paramref name="user"/> with its <c>Role</c> loaded; without it the token carries no permissions.
    /// </summary>
    string GenerateAccessToken(ApplicationUser user);

    /// <summary>
    /// The raw token goes to the client; only the hash is stored in the database.
    /// </summary>
    (string rawToken, string hashedToken) GenerateRefreshToken();

    /// <summary>
    /// SHA-256 hash (lowercase hex) of a raw refresh token, used to look up the stored token.
    /// </summary>
    string HashToken(string rawToken);
}
