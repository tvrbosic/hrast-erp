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

    public async Task<Result<AuthResponse>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
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

    public async Task<Result<AuthResponse>> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var hashedToken = tokenService.HashToken(refreshToken);

        var storedToken = await dbContext.Set<RefreshToken>()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken, ct);

        if (storedToken is null || !storedToken.IsActive)
            return AuthErrors.InvalidRefreshToken;

        // Revoke old token
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new pair
        var (rawNewToken, hashedNewToken) = tokenService.GenerateRefreshToken();
        storedToken.ReplacedByToken = hashedNewToken;

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

        var accessToken = tokenService.GenerateAccessToken(storedToken.User);

        return new AuthResponse(
            accessToken,
            rawNewToken,
            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes));
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hashedToken = tokenService.HashToken(refreshToken);

        var storedToken = await dbContext.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken, ct);

        if (storedToken is not null && !storedToken.IsRevoked)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(
        ApplicationUser user, CancellationToken ct)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var (rawRefreshToken, hashedRefreshToken) = tokenService.GenerateRefreshToken();

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
