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

        // A locked account answers exactly like a wrong password (same error, same hashing cost), so
        // lockout reveals neither that the account exists nor whether a guess was right.
        if (await userManager.IsLockedOutAsync(user))
        {
            userManager.PasswordHasher.VerifyHashedPassword(null!, DummyPasswordHash(), password);
            return AuthErrors.InvalidCredentials;
        }

        // Password before status, so only someone who knows the password learns the account is inactive.
        // Failures are counted towards lockout (Identity's Lockout options); a success resets the count.
        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return AuthErrors.InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        if (!user.IsActive || user.DeletedAt is not null)
            return AuthErrors.InactiveUser;

        // Login runs without the middleware, so an inactive tenant is refused here
        var tenantName = await FindLoginTenantNameAsync(user.TenantId, ct);
        if (tenantName is null)
            return AuthErrors.InactiveTenant;

        return await GenerateAuthTokensAsync(user, tenantName, ct);
    }

    // Ends every session of the user (all devices); used when refresh token reuse is detected.
    private Task<int> RevokeAllRefreshTokensAsync(Guid userId, CancellationToken ct) =>
        dbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct);

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

        if (storedToken is null || storedToken.IsExpired)
            return AuthErrors.InvalidRefreshToken;

        if (storedToken.IsRevoked)
        {
            // A rotated token (it has a successor) presented again means someone holds a copy. Revoking all of
            // the user's tokens also ends the other holder's chain. A token revoked by logout has no successor.
            if (storedToken.ReplacedByToken is not null)
                await RevokeAllRefreshTokensAsync(storedToken.UserId, ct);
            return AuthErrors.InvalidRefreshToken;
        }

        if (!storedToken.User.IsActive || storedToken.User.DeletedAt is not null)
            return AuthErrors.InactiveUser;

        // Checked before rotation, so a refused refresh leaves the token untouched (same as above).
        var tenantName = await FindLoginTenantNameAsync(storedToken.User.TenantId, ct);
        if (tenantName is null)
            return AuthErrors.InactiveTenant;

        // Build the response before rotating, so nothing can fail once the old token is revoked
        var accessToken = tokenService.GenerateAccessToken(storedToken.User);
        var user = ToAuthenticatedUser(storedToken.User, tenantName);
        var (rawNewToken, hashedNewToken) = tokenService.GenerateRefreshToken();

        // Revoke the old token only if it is still unrevoked, in one statement, inside a transaction with the
        // insert of its successor. A concurrent refresh with the same token waits on the row lock and then
        // matches no row, so exactly one request rotates it; the loser is treated as reuse.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var rotated = await dbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Where(rt => rt.Id == storedToken.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow)
                .SetProperty(rt => rt.ReplacedByToken, hashedNewToken), ct);
        if (rotated == 0)
        {
            await transaction.RollbackAsync(ct);
            await RevokeAllRefreshTokensAsync(storedToken.UserId, ct);
            return AuthErrors.InvalidRefreshToken;
        }

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
        await transaction.CommitAsync(ct);

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

    private async Task<AuthTokens> GenerateAuthTokensAsync(
        ApplicationUser user, string tenantName, CancellationToken ct)
    {
        // Build the response before saving, so nothing can fail once the refresh token is stored
        var accessToken = tokenService.GenerateAccessToken(user);
        var authenticatedUser = ToAuthenticatedUser(user, tenantName);
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

    // Returns the tenant's name, or null when its users may not log in or refresh (inactive, deleted or
    // missing). The super admin tenant is never refused, matching BlockInactiveEntityMiddleware.
    // IgnoreQueryFilters: login and refresh run without a tenant context.
    private Task<string?> FindLoginTenantNameAsync(Guid tenantId, CancellationToken ct) =>
        dbContext.Set<Tenant>()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId &&
                (t.Id == TenantConstants.SuperAdminTenantId || (t.IsActive && t.DeletedAt == null)))
            .Select(t => (string?)t.Name)
            .FirstOrDefaultAsync(ct);

    // Login and refresh build the authenticated user from the user they already loaded (with its role),
    // with the same permissions that go into the access token.
    private static AuthenticatedUser ToAuthenticatedUser(ApplicationUser user, string tenantName) =>
        new(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.TenantId,
            tenantName,
            user.RoleId,
            user.Role?.Name,
            PermissionMapper.ToNames(PermissionsInToken(user)));

    // Used by /me. Returns null when the user doesn't exist. IgnoreQueryFilters: the id comes from a
    // validated JWT, and a soft-deleted user is blocked by the middleware, not here. Permissions are
    // passed in rather than read from the role, so they always equal the ones in the caller's access token.
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
