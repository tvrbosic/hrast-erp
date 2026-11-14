using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Identity;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    HrastDbContext dbContext,
    ICurrentUser currentUser,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private readonly JwtSettings _settings = jwtOptions.Value;

    // Hash of a random password, made once with the configured hasher so verifying it costs the same as a real one.
    private static string? _dummyPasswordHash;

    /// <summary>
    /// Validates credentials and issues a new access/refresh token pair on success.
    /// </summary>
    public async Task<Result<AuthTokens>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        // Look up user and verify credentials.
        // IgnoreQueryFilters: no JWT exists yet at login, so tenant filter would exclude all users.
        // It also bypasses the soft-delete filter, so deleted users are rejected explicitly below.
        // Compared on NormalizedEmail, the column email uniqueness is checked on, so the case typed doesn't matter.
        var normalizedEmail = userManager.NormalizeEmail(email);
        var user = await userManager.Users
            .IgnoreQueryFilters()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (user is null)
        {
            // Hash anyway so an unknown email takes as long as a wrong password and response time
            // doesn't reveal which emails are registered.
            userManager.PasswordHasher.VerifyHashedPassword(null!, DummyPasswordHash(), password);
            return AuthErrors.InvalidCredentials;
        }

        // Password before status, so only someone who knows the password learns the account is inactive.
        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
            return AuthErrors.InvalidCredentials;

        if (!user.IsActive || user.DeletedAt is not null)
            return AuthErrors.InactiveUser;

        // Login runs without the middleware, so an inactive tenant is refused here. The super admin tenant
        // is never blocked, matching BlockInactiveEntityMiddleware.
        if (user.TenantId != TenantConstants.SuperAdminTenantId &&
            !await dbContext.Set<Tenant>().IgnoreQueryFilters()
                .AnyAsync(t => t.Id == user.TenantId && t.IsActive && t.DeletedAt == null, ct))
            return AuthErrors.InactiveTenant;

        return await GenerateAuthResponseAsync(user, ct);
    }

    private string DummyPasswordHash() =>
        _dummyPasswordHash ??= userManager.PasswordHasher.HashPassword(null!, Guid.NewGuid().ToString());

    /// <summary>
    /// Rotates the refresh token: revokes the old token and issues a new access/refresh token pair.
    /// </summary>
    public async Task<Result<AuthTokens>> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        // Hash inbound refresh token
        var hashedToken = tokenService.HashToken(refreshToken);

        // Verify that hashed token exists in database.
        // IgnoreQueryFilters: refresh tokens are looked up by token value, not tenant context.
        var storedToken = await dbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Include(rt => rt.User)
            .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken, ct);

        if (storedToken is null || !storedToken.IsActive)
            return AuthErrors.InvalidRefreshToken;

        if (!storedToken.User.IsActive || storedToken.User.DeletedAt is not null)
            return AuthErrors.InactiveUser;

        // Checked before rotation, so a refused refresh leaves the token untouched (same as above).
        var tenantId = storedToken.User.TenantId;
        if (tenantId != TenantConstants.SuperAdminTenantId &&
            !await dbContext.Set<Tenant>().IgnoreQueryFilters()
                .AnyAsync(t => t.Id == tenantId && t.IsActive && t.DeletedAt == null, ct))
            return AuthErrors.InactiveTenant;

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
            TenantId = storedToken.User.TenantId,
            Token = hashedNewToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays)
        };
        dbContext.Set<RefreshToken>().Add(newRefreshToken);
        await dbContext.SaveChangesAsync(ct);

        // Generate access token for user, with the role's current permissions
        var accessToken = tokenService.GenerateAccessToken(storedToken.User);

        // Load the authenticated user with the same permissions that went into the token
        var user = await GetAuthenticatedUserAsync(
            storedToken.UserId, PermissionsInToken(storedToken.User), ct);

        return new AuthTokens(
            accessToken,
            rawNewToken,
            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            user);
    }

    /// <summary>
    /// Revokes the provided refresh token, invalidating the session.
    /// </summary>
    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        // Hash inbound token and look up the stored entry
        var hashedToken = tokenService.HashToken(refreshToken);

        var storedToken = await dbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
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
    /// Returns the calling user with the permissions from their access token, not their role's current ones.
    /// </summary>
    public async Task<Result<AuthenticatedUser>> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var user = await FindAuthenticatedUserAsync(currentUser.UserId, currentUser.EffectivePermissions, ct);
        return user is not null ? user : AuthErrors.UserNotFound;
    }

    private async Task<AuthTokens> GenerateAuthResponseAsync(
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
            TenantId = user.TenantId,
            Token = hashedRefreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays)
        };

        dbContext.Set<RefreshToken>().Add(refreshToken);
        await dbContext.SaveChangesAsync(ct);

        // Load the authenticated user with the same permissions that went into the token
        var authenticatedUser = await GetAuthenticatedUserAsync(user.Id, PermissionsInToken(user), ct);

        return new AuthTokens(
            accessToken,
            rawRefreshToken,
            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            authenticatedUser);
    }

    // Must match what TokenService.GenerateAccessToken writes into the "permissions" claim,
    // so the authenticated user never shows permissions the token doesn't carry (or the reverse).
    private static Permission PermissionsInToken(ApplicationUser user) =>
        user.Role?.Permissions ?? Permission.None;

    // Called after the tokens are issued, so the user is known to exist; a missing user is a bug.
    private async Task<AuthenticatedUser> GetAuthenticatedUserAsync(
        Guid userId, Permission permissions, CancellationToken ct) =>
        await FindAuthenticatedUserAsync(userId, permissions, ct)
        ?? throw new InvalidOperationException($"User {userId} was not found after issuing its tokens.");

    // Returns null when the user doesn't exist. IgnoreQueryFilters: login and refresh run without a
    // tenant context, and the id always comes from a trusted source (verified credentials, a stored
    // refresh token or a validated JWT). Permissions are passed in rather than read from the role,
    // so they always equal the ones in the caller's access token.
    private async Task<AuthenticatedUser?> FindAuthenticatedUserAsync(
        Guid userId, Permission permissions, CancellationToken ct)
    {
        // Load the user with its role and tenant names in one query
        var user = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Join(
                dbContext.Set<Tenant>().IgnoreQueryFilters(),
                u => u.TenantId,
                t => t.Id,
                (u, t) => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.TenantId,
                    TenantName = t.Name,
                    u.RoleId,
                    RoleName = u.Role != null ? u.Role.Name : null
                })
            .FirstOrDefaultAsync(ct);

        // Convert the token's permission bitmask to the names clients use
        return user is null
            ? null
            : new AuthenticatedUser(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.TenantId,
                user.TenantName,
                user.RoleId,
                user.RoleName,
                PermissionMapper.ToNames(permissions));
    }
}
