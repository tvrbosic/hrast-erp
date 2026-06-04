using HrastERP.Infrastructure.Configuration;
using HrastERP.Infrastructure.Persistence;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Authentication;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    HrastDbContext dbContext,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    /// <summary>
    /// Validates credentials and issues a new access/refresh token pair on success.
    /// </summary>
    public async Task<Result<AuthResponse>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        // Look up user and verify credentials
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return AuthErrors.InvalidCredentials;

        if (!user.IsActive)
            return AuthErrors.InactiveUser;

        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
            return AuthErrors.InvalidCredentials;

        return await GenerateAuthResponseAsync(user, ct);
    }

    /// <summary>
    /// Rotates the refresh token: revokes the old token and issues a new access/refresh token pair.
    /// </summary>
    public async Task<Result<AuthResponse>> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        // Hash inbound refresh token
        var hashedToken = tokenService.HashToken(refreshToken);

        // Verify that hashed token exists in database
        var storedToken = await dbContext.Set<RefreshToken>()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken, ct);

        if (storedToken is null || !storedToken.IsActive)
            return AuthErrors.InvalidRefreshToken;

        // Revoke old token
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new refresh token pair
        var (rawNewToken, hashedNewToken) = tokenService.GenerateRefreshToken();
        storedToken.ReplacedByToken = hashedNewToken;

        // Write new refresh token entry into database
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            Token = hashedNewToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays)
        };
        dbContext.Set<RefreshToken>().Add(newRefreshToken);
        await dbContext.SaveChangesAsync(ct);

        // Generate access token for user
        var accessToken = tokenService.GenerateAccessToken(storedToken.User);

        return new AuthResponse(
            accessToken,
            rawNewToken,
            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes));
    }

    /// <summary>
    /// Revokes the provided refresh token, invalidating the session.
    /// </summary>
    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        // Hash inbound token and look up the stored entry
        var hashedToken = tokenService.HashToken(refreshToken);

        var storedToken = await dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken, ct);

        // Revoke if found and not already revoked; silently succeed otherwise
        if (storedToken is not null && !storedToken.IsRevoked)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    /// <summary>
    /// Generates an access token and persists a new refresh token for the given user.
    /// </summary>
    private async Task<AuthResponse> GenerateAuthResponseAsync(
        ApplicationUser user, CancellationToken ct)
    {
        // Generate token pair
        var accessToken = tokenService.GenerateAccessToken(user);
        var (rawRefreshToken, hashedRefreshToken) = tokenService.GenerateRefreshToken();

        // Persist refresh token
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = hashedRefreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays)
        };

        dbContext.Set<RefreshToken>().Add(refreshToken);
        await dbContext.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            rawRefreshToken,
            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes));
    }
}
