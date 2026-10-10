# Authorization

## Overview

Custom RBAC system built on ASP.NET Core policy-based authorization. At login, `TokenService` embeds the user's effective permissions as a single `long` claim in the JWT. `CurrentUser` reads that claim, and `PermissionAuthorizationHandler` performs a bitwise AND check when `[RequirePermission]` is applied to a controller action. No database query per request.

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

`Role` (`HrastERP.Infrastructure/Authorization/Entities/Role.cs`) is a plain entity with a `Permission Permissions` property. It does **not** inherit `BaseEntity<TId>` — roles are security configuration, not business entities, so they have no auditing, soft-delete, or tenant isolation.

`ApplicationUser` has a single nullable `Role` via `RoleId` FK. A user without a role gets `Permission.None` in the JWT. Deleting a role sets `RoleId = null` on all associated users (`OnDelete: SetNull`) rather than cascading deletes.

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
  └─ 200 OK or 403 Forbidden
```

## DI Registration

In `Program.cs`, after `AddAuthorization()`:

```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

`PermissionPolicyProvider` is singleton — policy construction uses only static enum values. `PermissionAuthorizationHandler` is scoped — it depends on scoped `ICurrentUser`.

## Out of Scope (Follow-up Sessions)

- **Role seeding** — startup idempotent seeder for predefined roles (Administrator, ProcurementOperator, ProductionWorker, WarehouseEmployee, FinanceEmployee)
- **Role management API** — CRUD commands/queries in `HrastERP.Administration`
- **Per-user permission overrides** — additional permissions on top of a user's role
