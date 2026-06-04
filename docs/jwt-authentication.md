# JWT Authentication

## Overview

The application uses JWT Bearer authentication with refresh token rotation. Access tokens are short-lived (15 min default), while refresh tokens are longer-lived (7 days default). Refresh tokens are stored as SHA-256 hashes in the database — raw tokens are never persisted.

## Configuration

JWT settings are defined in `appsettings.json` under the `"Jwt"` section and bound to `JwtSettings` (`Infrastructure/Configuration/JwtSettings.cs`):

| Property | Description | Default |
|----------|-------------|---------|
| `SecretKey` | HMAC-SHA256 signing key (min 32 characters) | — |
| `Issuer` | Token issuer | — |
| `Audience` | Token audience | — |
| `AccessTokenExpirationMinutes` | Access token lifetime | 15 |
| `RefreshTokenExpirationDays` | Refresh token lifetime | 7 |

Settings are validated at startup with `ValidateDataAnnotations()` + `ValidateOnStart()` — the app won't start with missing or invalid JWT config.

## Entities

- **`ApplicationUser`** — extends `IdentityUser<Guid>` with `TenantId`, `FirstName`, `LastName`, `IsActive`, and a `RefreshTokens` navigation collection.
- **`RefreshToken`** — stores hashed refresh tokens with `ExpiresAt`, `RevokedAt`, and `ReplacedByToken` (for rotation audit trail). Exposes computed `IsActive` = not expired AND not revoked.

## Services

- **`ITokenService` / `TokenService`** — generates JWT access tokens, generates refresh token pairs (raw + hash), and hashes raw tokens for DB lookup.
- **`IAuthService` / `AuthService`** — orchestrates login, refresh, and logout flows using Identity's `UserManager` and `ITokenService`.

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

## Core Logic

### Login (`POST /api/auth/login`)

1. Find user by email via `UserManager`
2. Reject if `IsActive` is false (`Auth.InactiveUser` error)
3. Verify password via `UserManager.CheckPasswordAsync`
4. Generate JWT access token with user claims
5. Generate refresh token pair (raw + SHA-256 hash)
6. Save the **hashed** refresh token to the database
7. Return `AuthResponse(accessToken, rawRefreshToken, expiresAt)`

### Refresh (`POST /api/auth/refresh`)

Uses **refresh token rotation** — each use invalidates the old token and issues a new pair:

1. Hash the incoming raw refresh token
2. Find the matching record in the database (include the `User` navigation)
3. Reject if not found or not active (`Auth.InvalidRefreshToken` error)
4. **Revoke** the old token (set `RevokedAt`)
5. Generate a new refresh token pair
6. Set `ReplacedByToken` on the old record (audit trail)
7. Save the new hashed token to the database
8. Generate a new access token for the user
9. Return new `AuthResponse` with both new tokens

### Logout (`POST /api/auth/logout`)

1. Hash the incoming raw refresh token
2. Find it in the database
3. If it exists and isn't already revoked, set `RevokedAt`
4. Always return success (idempotent)

## API Endpoints

| Endpoint | Body | Success | Failure |
|----------|------|---------|---------|
| `POST /api/auth/login` | `{ email, password }` | `200` + `AuthResponse` | `401` |
| `POST /api/auth/refresh` | `{ refreshToken }` | `200` + `AuthResponse` | `401` |
| `POST /api/auth/logout` | `{ refreshToken }` | `204` | `400` |

`AuthResponse` shape: `{ accessToken, refreshToken, expiresAt }`.

## Middleware Pipeline

Configured in `Program.cs`:

1. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` — sets JWT Bearer as the default scheme
2. `AddJwtBearer` — configures token validation (issuer, audience, signing key, lifetime, `ClockSkew = TimeSpan.Zero`)
3. `app.UseAuthentication()` — reads the `Authorization: Bearer <token>` header, validates the JWT, and populates `HttpContext.User` with the claims
4. `app.UseAuthorization()` — evaluates `[Authorize]` attributes against the authenticated user

## Claim Extraction and Usage

`CurrentUser` and `CurrentTenant` are thin wrappers around `HttpContext.User` (the `ClaimsPrincipal` populated by the JWT middleware). They implement `ICurrentUser` / `ICurrentTenant` and are registered as scoped services in `Program.cs` — one instance per HTTP request.

Each property extracts a specific claim and falls back to a safe default when the claim is missing (unauthenticated requests):

| Property | Claim | Fallback |
|----------|-------|----------|
| `UserId` | `sub` (`ClaimTypes.NameIdentifier`) | `Guid.Empty` |
| `TenantId` | `tenant_id` | `Guid.Empty` |
| `Username` | `email` (`ClaimTypes.Email`) | `string.Empty` |
| `IsAuthenticated` | `HttpContext.User.Identity.IsAuthenticated` | `false` |

`CurrentUser` is the **only** place in the codebase that touches `HttpContext`. Everything above it depends solely on `ICurrentUser` / `ICurrentTenant`, keeping application-layer handlers unaware of HTTP or JWTs:

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
  |    expiresAt }             |                              |
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
  |    expiresAt }             |                              |
  |                            |                              |
  +-- POST /api/auth/logout -->|                              |
  |  { refreshToken }          +-- Revoke token in DB         |
  |<-- 204 No Content ---------+                              |
```

## Security Properties

- **Hashed storage** — refresh tokens are stored as SHA-256 hashes, never raw
- **Token rotation** — each refresh invalidates the old token and issues a new one
- **No clock skew** — `ClockSkew = TimeSpan.Zero`, tokens expire exactly on time
- **Short-lived access tokens** — 15 min default, limits exposure window
- **Inactive user check** — deactivated accounts are rejected at login
