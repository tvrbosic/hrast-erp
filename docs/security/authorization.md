# Authorization

## Overview

Custom RBAC system built on ASP.NET Core policy-based authorization. At login, `TokenService` embeds the user's effective permissions as a single `long` claim in the JWT. `CurrentUser` reads that claim, and `PermissionAuthorizationHandler` performs a bitwise AND check when `[RequirePermission]` is applied to a controller action. No database query per request.

New to this? Read the step-by-step walkthrough in [permission-checks-guide.md](permission-checks-guide.md) first — it covers bit flags, the attribute → policy → handler flow, and AND vs OR semantics.

## Protecting an Endpoint

Apply `[RequirePermission]` to a controller action or controller class:

```csharp
[RequirePermission(Permission.FinanceView)]
[HttpGet("invoices")]
public async Task<IActionResult> GetInvoices() { ... }
```

The attribute supports `AllowMultiple = true`, so you can stack multiple requirements:

```csharp
[RequirePermission(Permission.FinanceView)]
[RequirePermission(Permission.InventoryView)]
[HttpGet("report")]
public async Task<IActionResult> GetReport() { ... }
```

A request missing the `Authorization` header returns `401 Unauthorized`. A valid JWT where the user lacks the required permission returns `403 Forbidden`.

## Permission Enum

`Permission` is a `[Flags] enum : long` in `HrastERP.SharedKernel/Authorization/Permission.cs`. Each value is a unique power-of-two bit:

| Module | Bits | Permissions |
|---|---|---|
| Administration | 0–3 | `AdministrationView/Create/Edit/Delete` |
| Finance | 4–7 | `FinanceView/Create/Edit/Delete` |
| Inventory | 8–11 | `InventoryView/Create/Edit/Delete` |
| Procurement | 12–15 | `ProcurementView/Create/Edit/Delete` |
| Production | 16–19 | `ProductionView/Create/Edit/Delete` |

`None = 0` is defined so `ToString()` returns `"None"` for a user with no role. Bits 20–63 are reserved for future modules.

## Role Entity

`Role` (`HrastERP.Infrastructure/Identity/Entities/Role.cs`) is a plain entity with a `Permission Permissions` property. It does **not** inherit `BaseEntity<TId>` — roles are security configuration, not business entities, so they have no auditing, soft-delete, or tenant isolation.

`ApplicationUser` has a single nullable `Role` via `RoleId` FK. A user without a role gets `Permission.None` in the JWT. The FK uses `OnDelete: Restrict` — a role that is still assigned to any user (in any tenant, active or not) cannot be deleted. The `DeleteRole` handler checks this first and returns `409 Role.InUse`; the FK is defense in depth — if a user is assigned concurrently, the FK still blocks the delete and the request fails with a 500.

Deleting a user (`DELETE /api/admin/users/{id}`) keeps their `RoleId`. `DeleteRole` counts soft-deleted users too, so the role can't be deleted until `SoftDeleteCleanupJob` purges them (`409 Role.InUse`). Outside the super admin tenant, the caller may only delete users whose role permissions they hold (`403 User.DeleteExceedsOwnPermissions`).

## Role Management API

Roles are global (shared by all tenants). Any user with `AdministrationView` can read them — tenant admins need this to assign roles to users — but only super admin tenant users can change them.

| Endpoint | Authorization | Success |
|---|---|---|
| `GET /api/admin/roles?page=&pageSize=` | `AdministrationView` | 200 paged `RoleResponse` list |
| `GET /api/admin/roles/{id}` | `AdministrationView` | 200 `RoleResponse` |
| `POST /api/admin/roles` | `[RequireSuperAdminTenant]` + `AdministrationCreate` | 201 `{ data: Guid }` + `Location` |
| `PUT /api/admin/roles/{id}` | `[RequireSuperAdminTenant]` + `AdministrationEdit` | 204 |
| `DELETE /api/admin/roles/{id}` | `[RequireSuperAdminTenant]` + `AdministrationDelete` | 204 |
| `GET /api/admin/permissions` | `AdministrationView` | 200 permissions grouped by module |

- `[RequireSuperAdminTenant]` is applied **per write action**, not on the controller, so reads stay open to tenant admins.
- Permissions travel over the API as enum **names** (`["FinanceView", "InventoryEdit"]`), not as the numeric bitmask. `PermissionMapper` (`HrastERP.SharedKernel/Authorization/`) converts between the two. Unknown or wrongly-cased names fail validation with `422` and an indexed field error (`permissions[1]`).
- `page` must be ≥ 1 and `pageSize` between 1 and 100 (`422` otherwise).
- Role names are trimmed and unique, case-insensitively (`409 Role.NameTaken`). Uniqueness is enforced by a unique index on `Role.NormalizedName` (trimmed, `ToUpperInvariant()`, like Identity's `NormalizedUserName`); a concurrent duplicate that slips past the handler's check is rejected by the index and fails with a 500.
- **Assignment ceiling:** outside the super admin tenant, `CreateUser`/`UpdateUser` only accept a `RoleId` whose permissions the caller holds (`403 Role.ExceedsOwnPermissions`), and a caller can't change the role of a user who holds permissions they lack (`403 User.RoleExceedsOwnPermissions`). This stops a tenant admin from granting themselves or others more than they have. Super admin tenant users are exempt.
- **Administrator protection:** the seeded Administrator role (`RoleConstants.AdministratorRoleId`) cannot be deleted and must always hold every permission (`409 Role.AdministratorImmutable`). It can still be renamed or have its description changed.
- **Propagation:** permissions are embedded in the JWT, so a change to a role reaches its users on their next token refresh — at most `AccessTokenExpirationMinutes` (15 by default).

## How Permissions Reach the JWT

1. `AuthService.LoginAsync` / `RefreshAsync` eager-loads `Role` via `.Include(u => u.Role)` before calling `TokenService`
2. `TokenService.GenerateAccessToken` writes `"permissions": "<long>"` into the JWT — the combined flags as a decimal string
3. `CurrentUser.EffectivePermissions` parses the claim back to `Permission` via `long.TryParse` + cast; falls back to `Permission.None` on malformed or absent claims

## Request Evaluation Flow

```
[RequirePermission(Permission.FinanceView)]
  └─ Sets policy name "Permission:16"
  └─ PermissionPolicyProvider builds AuthorizationPolicy with PermissionRequirement(FinanceView)
  └─ PermissionAuthorizationHandler checks (EffectivePermissions & FinanceView) != 0
  └─ 200 OK, or 403 General.Forbidden (ErrorResponse, written by ForbiddenResponseHandler)
```

## DI Registration

In `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
    options.AddPolicy(RequireSuperAdminTenantAttribute.PolicyName, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new SuperAdminTenantRequirement())));
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SuperAdminTenantAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenResponseHandler>();
```

`PermissionPolicyProvider` is singleton — policy construction uses only static enum values. The two authorization handlers are scoped — they depend on scoped `ICurrentUser` / `ICurrentTenant`.

## Folder layout

`HrastERP.API/Authorization/` is one namespace (`HrastERP.API.Authorization`); each subfolder holds everything one policy needs, and the root holds what all policies share:

| Path | Contents |
|---|---|
| `Permission/` | `RequirePermissionAttribute`, `PermissionRequirement`, `PermissionAuthorizationHandler`, `PermissionPolicyProvider` (builds `Permission:<long>` policies on demand) |
| `SuperAdminTenant/` | `RequireSuperAdminTenantAttribute`, `SuperAdminTenantRequirement`, `SuperAdminTenantAuthorizationHandler` (a fixed policy registered in `Program.cs`) |
| `ForbiddenResponseHandler` | Writes every 403 from a failed policy as an `ErrorResponse`: the first `ErrorFailureReason`'s error, otherwise `General.Forbidden`. Challenges keep the bodiless 401. |
| `ErrorFailureReason` | Failure reason carrying an `Error`; a handler fails with it to return a specific 403 code (e.g. `Tenant.SuperAdminRequired`). |

A new policy gets its own subfolder (attribute, requirement, handler) and, if it needs a specific 403 code, fails with an `ErrorFailureReason` whose `Error` lives in `ApiErrors`.

## Out of Scope (Follow-up Sessions)

- **Per-user permission overrides** — additional permissions on top of a user's role
