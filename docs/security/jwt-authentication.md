# JWT Authentication

## Overview

The application uses JWT Bearer authentication with refresh token rotation. Access tokens are short-lived (15 min default), while refresh tokens are longer-lived (7 days default). Refresh tokens are stored as SHA-256 hashes in the database — raw tokens are never persisted.

## Configuration

JWT settings are defined in `appsettings.json` under the `"Jwt"` section and bound to `JwtSettings` (`Infrastructure/Identity/JwtSettings.cs`):

| Property | Description | Default |
|----------|-------------|---------|
| `SecretKey` | HMAC-SHA256 signing key (min 32 characters) | — |
| `Issuer` | Token issuer | — |
| `Audience` | Token audience | — |
| `AccessTokenExpirationMinutes` | Access token lifetime | 15 |
| `RefreshTokenExpirationDays` | Refresh token lifetime | 7 |

Settings are validated at startup with `ValidateDataAnnotations()` + `ValidateOnStart()` — the app won't start with missing or invalid JWT config.

## Entities

- **`ApplicationUser`** — extends `IdentityUser<Guid>` with `TenantId`, `FirstName`, `LastName`, `IsActive`, a `RefreshTokens` navigation collection, and a nullable `RoleId` FK + `Role` navigation (single role per user). It also implements `IAuditable` and `ISoftDeletable`: `CreatedAt`/`CreatedBy`, `UpdatedAt`/`UpdatedBy` and `DeletedAt`/`DeletedBy`, populated by the interceptors. Soft-deleted users are hidden by the global query filter.
- **`RefreshToken`** — stores hashed refresh tokens with `ExpiresAt`, `RevokedAt`, and `ReplacedByToken` (for rotation audit trail). Exposes computed `IsActive` = not expired AND not revoked.

## Services

- **Identity registration** — `AddIdentityServices()` (`Infrastructure/Identity/IdentityServiceExtensions.cs`) sets up `AddIdentityCore<ApplicationUser>` with the password and user rules, `AddEntityFrameworkStores<HrastDbContext>()`, and `AddDefaultTokenProviders()` plus `AddDataProtection()`. The token providers create the password reset tokens that the admin ChangePassword command uses, and Data Protection encrypts those tokens. Without them, `GeneratePasswordResetTokenAsync` throws and the endpoint returns a 500.
- **`ITokenService` / `TokenService`** — generates JWT access tokens, generates refresh token pairs (raw + hash), and hashes raw tokens for DB lookup.
- **`IAuthService` / `AuthService`** — orchestrates login, refresh, logout and the current user (`/me`) using Identity's `UserManager` and `ITokenService`. All of it lives in `Infrastructure/Identity/` (`Entities/`, `Persistence/`, `Services/`), namespace `HrastERP.Infrastructure.Identity`.

## Access Token Claims

When `TokenService.GenerateAccessToken` creates a JWT, it includes:

| Claim | Source |
|-------|--------|
| `sub` | `ApplicationUser.Id` |
| `email` | `ApplicationUser.Email` |
| `tenant_id` | `ApplicationUser.TenantId` |
| `given_name` | `ApplicationUser.FirstName` |
| `family_name` | `ApplicationUser.LastName` |
| `jti` | Random GUID (unique token ID) |
| `permissions` | `ApplicationUser.Role.Permissions` as `long` (`0` when no role is assigned) |

## Core Logic

### Login (`POST /api/auth/login`)

1. Find user by `NormalizedEmail` (case-insensitive) via `userManager.Users.Include(u => u.Role)` (eager-loads role so permissions are available for JWT generation). If none is found, verify the password against a dummy hash anyway, so response time doesn't reveal which emails exist, and return `Auth.InvalidCredentials`
2. Verify password via `UserManager.CheckPasswordAsync` (`Auth.InvalidCredentials`). A locked-out account (`IsLockedOutAsync`) gets the same `Auth.InvalidCredentials` after a dummy hash, so lockout reveals nothing; a wrong password calls `AccessFailedAsync` (5 failures in a row lock the account for 15 minutes, set in `AddIdentityServices()`), a correct one calls `ResetAccessFailedCountAsync`
3. Reject if `IsActive` is false or the user is soft-deleted (`Auth.InactiveUser`). This comes after the password check, so only someone who knows the password learns the account is inactive
4. Reject if the user's tenant is inactive or soft-deleted (`Auth.InactiveTenant`); the super admin tenant is never blocked, matching `BlockInactiveEntityMiddleware`. Login runs without the middleware, so this check lives here. The same query loads the tenant's name for the response
5. Generate JWT access token with user claims
6. Build the authenticated user (see below) from the loaded user, role and tenant name, with the permissions that went into the token
7. Generate refresh token pair (raw + SHA-256 hash)
8. Save the **hashed** refresh token to the database. Nothing that can fail runs after this
9. Return `AuthTokens(accessToken, rawRefreshToken, expiresAt, user)`

### Refresh (`POST /api/auth/refresh`)

Uses **refresh token rotation** — each use invalidates the old token and issues a new pair:

1. Hash the incoming raw refresh token
2. Find the matching record in the database (include `User` + `ThenInclude(u => u.Role)` so permissions are available for JWT generation)
3. Reject if not found or expired (`Auth.InvalidRefreshToken` error)
4. If it is revoked, reject with `Auth.InvalidRefreshToken`. A revoked token that has a `ReplacedByToken` was rotated, so presenting it again is **reuse**: someone holds a copy. All of the user's active refresh tokens are revoked (every session on every device), which also ends the other holder's chain. A token revoked by logout has no successor and only gets the error
5. Reject if the user is inactive or deleted (`Auth.InactiveUser`) or the user's tenant is inactive or deleted (`Auth.InactiveTenant`, never for the super admin tenant); the token is not rotated. The tenant query also loads the tenant's name
6. Generate a new access token and build the authenticated user from the loaded user, role and tenant name, with the permissions that went into the token. This happens before rotation, so nothing can fail once the old token is revoked
7. Generate a new refresh token pair
8. In one transaction: **revoke** the old token with a single `UPDATE … WHERE revoked_at IS NULL` that also sets `ReplacedByToken`, then save the new hashed token. A concurrent refresh with the same token waits on the row lock and updates no row; it is treated as reuse (step 4). So exactly one request rotates a token
9. Return new `AuthTokens` with both new tokens and the user

Clients must not refresh the same token twice in parallel (e.g. two browser tabs): the second request counts as reuse and logs the user out everywhere. Share one refresh across tabs.

### Logout (`POST /api/auth/logout`)

1. Hash the incoming raw refresh token
2. Find it in the database
3. If it exists and isn't already revoked, set `RevokedAt`
4. Always return success (idempotent)

### Current user (`GET /api/auth/me`)

`[Authorize]` only — any authenticated user can read their own data, so there is no `[RequirePermission]`. `AuthService.GetCurrentUserAsync` loads the user by `ICurrentUser.UserId` and passes `ICurrentUser.EffectivePermissions` (the caller's token) as the permissions. It adds no checks of its own: `BlockInactiveEntityMiddleware` already returns `403` `Auth.InactiveUser` for inactive or deleted users (including super admin tenant users) and `403` `Auth.InactiveTenant` for inactive tenants. A user that no longer exists gives `404 Auth.UserNotFound`.

### Authenticated user

Login, refresh and `/me` return the same `AuthenticatedUser`. Login and refresh build it from the user and role they already loaded, plus the tenant name from their tenant check, before anything is saved, so a failure can't leave a stored or revoked token behind. `/me` loads it with a single query that joins the user to its tenant (inner) and role (left) with `IgnoreQueryFilters()`; the user id comes from a validated JWT.

**Permission rule: the authenticated user's `permissions` are always the permissions in the access token that goes with it.** Login and refresh issue a token from the role's current permissions (`user.Role?.Permissions ?? Permission.None`) and build the user from that same value. `/me` issues no token and passes the caller's token permissions, so after a role change it shows the old permissions until the next refresh (at most `AccessTokenExpirationMinutes`) — exactly what the API enforces meanwhile. Every other field comes from the database, so in that window `/me` can show a new role name with the old permissions. The UI can therefore trust `permissions` to decide which menus and buttons to show, without decoding the JWT or knowing the `Permission` bit layout.

## API Endpoints

| Endpoint | Body | Success | Failure |
|----------|------|---------|---------|
| `POST /api/auth/login` | `{ email, password }` | `200` + `AuthTokens` | `401` `Auth.InvalidCredentials` (unknown email, wrong password or locked account), `403` `Auth.InactiveUser`, `403` `Auth.InactiveTenant` |
| `POST /api/auth/refresh` | `{ refreshToken }` | `200` + `AuthTokens` | `401` `Auth.InvalidRefreshToken` (unknown, revoked or expired), `403` `Auth.InactiveUser`, `403` `Auth.InactiveTenant` |
| `POST /api/auth/logout` | `{ refreshToken }` | `204` | none from the service (always succeeds) |
| `GET /api/auth/me` | — (bearer token) | `200` + `AuthenticatedUser` | `401` `General.Unauthorized` (no or invalid token) / `Auth.TokenExpired`, `403` `Auth.InactiveUser` / `Auth.InactiveTenant` (middleware), `404` `Auth.UserNotFound` |

A missing or malformed body field fails model binding on login, refresh or logout with `422` `General.Validation`. All failures use the flat `ErrorResponse` shape.

`AuthTokens` shape (inside the `data` envelope): `{ accessToken, refreshToken, expiresAt, user }`.

`AuthenticatedUser` shape (`user` above, and the `data` of `/me`):

```json
{
  "id": "…", "email": "…", "firstName": "…", "lastName": "…",
  "tenantId": "…", "tenantName": "…",
  "roleId": "…", "roleName": "…",
  "permissions": ["AdministrationView", "FinanceView"]
}
```

`roleId` / `roleName` are `null` for a user without a role, and `permissions` is then `[]`. Permissions are enum member names ordered by bit (never `None`), the same format the roles API uses.

## Middleware Pipeline

Services are registered by `AddApiAuthentication()` (`HrastERP.API/Authentication/AuthenticationServiceExtensions.cs`, called from `Program.cs`); the middleware is added in `Program.cs`:

1. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` — sets JWT Bearer as the default scheme
2. `AddJwtBearer` — configures token validation (issuer, audience, signing key, lifetime, `ClockSkew = TimeSpan.Zero`)
3. `app.UseAuthentication()` — reads the `Authorization: Bearer <token>` header, validates the JWT, and populates `HttpContext.User` with the claims
4. `app.UseAuthorization()` — evaluates `[Authorize]` attributes against the authenticated user

## Claim Extraction and Usage

`CurrentUser` and `CurrentTenant` are thin wrappers around `HttpContext.User` (the `ClaimsPrincipal` populated by the JWT middleware). They implement `ICurrentUser` / `ICurrentTenant` and are registered as scoped services by `AddApiAuthentication()` — one instance per HTTP request.

Custom claim names come from `JwtClaimNames` (`Infrastructure/Identity`), shared with `TokenService`, which writes them. Each property extracts a specific claim and falls back to a safe default when the claim is missing (unauthenticated requests):

| Property | Claim | Fallback |
|----------|-------|----------|
| `UserId` | `sub` (`ClaimTypes.NameIdentifier`) | `Guid.Empty` |
| `TenantId` | `tenant_id` | `Guid.Empty` |
| `Username` | `email` (`ClaimTypes.Email`) | `string.Empty` |
| `IsAuthenticated` | `HttpContext.User.Identity.IsAuthenticated` | `false` |
| `EffectivePermissions` | `permissions` (parsed as `long`, cast to `Permission`) | `Permission.None` |

`CurrentUser` is the **only** place in the codebase that touches `HttpContext`. Everything above it depends solely on `ICurrentUser` / `ICurrentTenant`, keeping application-layer handlers unaware of HTTP or JWTs:

### Why `IHttpContextAccessor` is always available

`AddHttpContextAccessor()` (called by `AddApiAuthentication()`) registers `IHttpContextAccessor` as a singleton. ASP.NET Core guarantees that `HttpContext` is set on the accessor for the lifetime of any HTTP request.

`CurrentUser` and `CurrentTenant` are scoped — one instance per request — so `HttpContext` is always live when they are resolved. `HrastDbContext` is also scoped, and it captures `TenantId` once at construction time (the `CurrentTenantId` property). This is safe because the `DbContext` never outlives the request that created it.

The only case where `HttpContext` is `null` is outside an HTTP request (background jobs, tests). Both classes handle this defensively by falling back to `Guid.Empty` / `string.Empty` rather than throwing.

```csharp
public class MyCommandHandler(ICurrentUser currentUser) : IRequestHandler<MyCommand, Result>
{
    public async Task<Result> Handle(MyCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;     // Guid
        var tenantId = currentUser.TenantId; // Guid
        var email = currentUser.Username;    // string
    }
}
```

## Request Flow Diagram

```
Client                        API                         Infrastructure
  |                            |                              |
  +-- POST /api/auth/login --->|                              |
  |  { email, password }       +-- AuthService.LoginAsync --->|
  |                            |  +-- UserManager.FindByEmail |
  |                            |  +-- CheckPassword           |
  |                            |  +-- GenerateAccessToken (JWT)
  |                            |  +-- GenerateRefreshToken    |
  |                            |  +-- Save hashed refresh to DB
  |<-- { accessToken,         |<------------------------------+
  |    refreshToken,           |                              |
  |    expiresAt, user }       |                              |
  |                            |                              |
  +-- GET /api/resource ------>|                              |
  |  Authorization: Bearer JWT |                              |
  |                            +-- JWT middleware validates    |
  |                            +-- HttpContext.User populated  |
  |                            +-- CurrentUser reads claims    |
  |                            +-- Handler uses ICurrentUser   |
  |<-- response ---------------+                              |
  |                            |                              |
  +-- POST /api/auth/refresh ->|  (when access token expires) |
  |  { refreshToken }          +-- AuthService.RefreshAsync ->|
  |                            |  +-- Hash token, find in DB  |
  |                            |  +-- Revoke old token        |
  |                            |  +-- Generate new pair       |
  |                            |  +-- Save new to DB          |
  |<-- { new accessToken,     |<------------------------------+
  |    new refreshToken,       |                              |
  |    expiresAt, user }       |                              |
  |                            |                              |
  +-- POST /api/auth/logout -->|                              |
  |  { refreshToken }          +-- Revoke token in DB         |
  |<-- 204 No Content ---------+                              |
```

## Security Properties

- **Hashed storage** — refresh tokens are stored as SHA-256 hashes, never raw
- **Token rotation** — each refresh invalidates the old token and issues a new one, atomically
- **Reuse detection** — presenting an already rotated token revokes all of the user's refresh tokens
- **No clock skew** — `ClockSkew = TimeSpan.Zero`, tokens expire exactly on time
- **Short-lived access tokens** — 15 min default, limits exposure window
- **Account lockout** — Identity lockout: 5 failed logins in a row lock the account for 15 minutes; a locked account gets the same response as a wrong password
- **Inactive user and tenant check** — deactivated and deleted accounts, and users of deactivated or deleted tenants, are rejected at login and token refresh
- **No caching of tokens** — login and refresh responses send `Cache-Control: no-store` (`[ResponseCache(NoStore = true)]`), so browsers and proxies never store them
