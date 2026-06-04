using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HrastERP.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HrastERP.Infrastructure.Authentication;

internal sealed class TokenService(IOptions<JwtSettings> jwtOptions) : ITokenService
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    /// <summary>
    /// Generates a signed JWT access token for the given user, embedding identity and tenant claims.
    /// </summary>
    /// <param name="user">The authenticated user for whom the token is issued.</param>
    /// <returns>A compact serialized JWT string.</returns>
    public string GenerateAccessToken(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically random refresh token and its SHA-256 hash.
    /// Only the hash is stored in the database; the raw token is sent to the client.
    /// </summary>
    /// <returns>A tuple of the raw Base64 token and its lowercase hex SHA-256 hash.</returns>
    public (string rawToken, string hashedToken) GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(randomBytes);
        var hashedToken = HashToken(rawToken);
        return (rawToken, hashedToken);
    }

    /// <summary>
    /// Computes a SHA-256 hash of the given token string for safe storage and comparison.
    /// </summary>
    /// <param name="rawToken">The plaintext token to hash.</param>
    /// <returns>A lowercase hex-encoded SHA-256 hash of the token.</returns>
    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(bytes);
    }
}
