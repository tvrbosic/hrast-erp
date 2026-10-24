# Permission Checks — Developer Guide

This guide explains **how** a permission check works in HrastERP, from the bit layout of the `Permission` enum to the `403 Forbidden` a user gets when they lack access. For the concise reference (DI registration, role entity, out-of-scope items), see [authorization.md](authorization.md).

## 1. Permissions are bits

`Permission` (`HrastERP.SharedKernel/Authorization/Permission.cs`) is a `[Flags] enum : long`. Every permission occupies **exactly one bit** of a 64-bit number, so a single `long` can hold any combination of permissions.

```csharp
[Flags]
public enum Permission : long
{
    None = 0,

    AdministrationView   = 1L << 0,   // 0b0001 = 1
    AdministrationCreate = 1L << 1,   // 0b0010 = 2
    AdministrationEdit   = 1L << 2,   // 0b0100 = 4
    AdministrationDelete = 1L << 3,   // 0b1000 = 8
    // ...
}
```

Think of the number as a row of light switches — each permission owns one switch:

```
bit position:   3    2    1    0
worth:          8    4    2    1
              ┌────┬────┬────┬────┐
              │ D  │ E  │ C  │ V  │   (Delete, Edit, Create, View)
              └────┴────┴────┴────┘
```

### Why powers of two (1, 2, 4, 8 …)?

A bit at position `n` is worth 2ⁿ in decimal. Powers of two are the only numbers with **a single bit set**, which guarantees that no two permissions overlap and every combination produces a unique number.

Counter-example — what goes wrong with sequential values:

```csharp
View = 1,    // 0001
Create = 2,  // 0010
Edit = 3,    // 0011  ← two bits set!

View | Create == 0011 == 3 == Edit   // user silently gains Edit
```

With powers of two, `View | Create = 0011 = 3` and `Edit = 0100 = 4` — no collision.

### Why `1L << n` instead of writing 1, 2, 4, 8?

The values are identical; the shift form is a readability choice:

- **The bit position is visible** — `ProductionEdit = 1L << 18` tells you it's bit 18; `262144` doesn't.
- **Gaps and duplicates stand out** — shift amounts run 0, 1, 2 … 19.
- **Adding a permission is trivial** — the next one is `1L << 20`.

### Why `1L` and not `1`?

`1` is a 32-bit `int`. `1 << 31` lands on the sign bit (negative), and `1 << 32` wraps back to `1` because C# masks `int` shift counts to 5 bits — the new permission would silently equal `AdministrationView`. `1L` forces 64-bit arithmetic, matching the enum's `long` underlying type and giving room for 64 permissions.

### What `[Flags]` does

It doesn't change any values. It changes `ToString()` so a combined value prints as `"FinanceView, FinanceEdit"` instead of `"80"` — useful in logs and the debugger. `None = 0` represents "no permissions"; never give a real permission the value 0, because `x & 0` is always 0 and the check could never pass.

## 2. Combining and checking with bitwise operators

| Operation | Operator | Example |
|---|---|---|
| Combine permissions | `\|` (OR) | `Permission.FinanceView \| Permission.FinanceEdit` → `0b0101_0000` = 80 |
| Check a permission | `&` (AND) | `(perms & Permission.FinanceEdit) != 0` → "is the Edit switch on?" |

```csharp
var perms = Permission.FinanceView | Permission.FinanceEdit;   // 80

(perms & Permission.FinanceEdit)   != 0   // true
(perms & Permission.FinanceDelete) != 0   // false
```

### Seeded roles decoded

Role permissions are stored as a single `bigint` in the `Roles` table (`Reference/0001_roles.sql`). Decoded:

| Role | Stored value | Hex | Permissions |
|---|---|---|---|
| Administrator | 1048575 | `0xFFFFF` | All 20 bits (0–19) |
| ProcurementOperator | 61696 | `0xF100` | Procurement CRUD + InventoryView |
| ProductionWorker | 983296 | `0xF0100` | Production CRUD + InventoryView |
| WarehouseEmployee | 7936 | `0x1F00` | Inventory CRUD + ProcurementView |
| FinanceEmployee | 4592 | `0x11F0` | Finance CRUD + InventoryView + ProcurementView |

Hex is the easiest way to read these: each hex digit is one module's 4 bits (`F` = all four CRUD bits, `1` = View only).

## 3. Where a user's permissions come from

No database query happens per request. Permissions travel inside the JWT:

1. **Login / refresh** — `AuthService` loads the user with `.Include(u => u.Role)`.
2. **Token generation** — `TokenService` writes the role's flags as a decimal string claim:
   ```csharp
   new Claim("permissions", ((long)(user.Role?.Permissions ?? Permission.None)).ToString())
   ```
3. **Each request** — `CurrentUser.EffectivePermissions` (`HrastERP.API/Authentication/CurrentUser.cs`) parses the claim back:
   ```csharp
   public Permission EffectivePermissions =>
       long.TryParse(User?.FindFirstValue("permissions"), out var raw)
           ? (Permission)raw
           : Permission.None;
   ```

A user without a role, or with a missing/malformed claim, gets `Permission.None` and fails every check.

> Because permissions are baked into the token, changing a user's role takes effect only after their access token is refreshed.

## 4. The moving parts

All four types live in `HrastERP.API/Authorization/` and plug into ASP.NET Core's policy-based authorization.

| Type | Role |
|---|---|
| `RequirePermissionAttribute` | Subclass of `[Authorize]`. Encodes the permission into a policy name: `[RequirePermission(Permission.FinanceView)]` ≡ `[Authorize(Policy = "Permission:16")]`. |
| `PermissionPolicyProvider` | Intercepts policy names starting with `"Permission:"`, parses the number, and builds a policy on the fly (`RequireAuthenticatedUser()` + `PermissionRequirement`). Other policy names fall through to the default provider. This is why no policies are registered by hand. |
| `PermissionRequirement` | Pure data carrier: "this request needs permission X". No logic. |
| `PermissionAuthorizationHandler` | Performs the actual check: `(currentUser.EffectivePermissions & requirement.Permission) != 0`. |

Registered in `Program.cs`:

```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

The provider is a singleton (it only uses static enum values); the handler is scoped (it depends on scoped `ICurrentUser`).

## 5. End-to-end request flow

```
GET /api/admin/users          JWT claim: permissions = 15 (Administration CRUD)
        │
        ▼
[RequirePermission(Permission.AdministrationView)]
        │   policy name = "Permission:1"
        ▼
PermissionPolicyProvider
        │   builds policy: authenticated user + PermissionRequirement(AdministrationView)
        ▼
PermissionAuthorizationHandler
        │   EffectivePermissions = 15 = 0b1111
        │   required             =  1 = 0b0001
        │   15 & 1 = 1 ≠ 0  →  context.Succeed()
        ▼
Controller action runs
```

Outcomes:

| Situation | Response |
|---|---|
| No / invalid JWT | `401 Unauthorized` |
| Valid JWT, permission bit not set | `403 Forbidden` |
| Valid JWT, permission bit set | Action executes |

## 6. Protecting endpoints

Apply the attribute to an action or a whole controller:

```csharp
[ApiController]
[Route("api/admin/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    public async Task<IActionResult> GetAll(...) { ... }

    [RequirePermission(Permission.AdministrationCreate)]
    [HttpPost]
    public async Task<IActionResult> Create(...) { ... }
}
```

Convention: map HTTP verbs to CRUD permissions — `GET` → `View`, `POST` → `Create`, `PUT`/`PATCH` → `Edit`, `DELETE` → `Delete`.

### AND vs OR — read this carefully

| Syntax | Semantics | Why |
|---|---|---|
| Stacked attributes | **AND** — user needs all of them | Each attribute is a separate policy; every policy must pass (`AllowMultiple = true`). |
| Combined flags in one attribute | **OR** — any one is enough | The handler checks `& != 0`, which passes if *any* bit overlaps. |

```csharp
// AND: requires FinanceView AND InventoryView
[RequirePermission(Permission.FinanceView)]
[RequirePermission(Permission.InventoryView)]

// OR: requires FinanceView OR InventoryView
[RequirePermission(Permission.FinanceView | Permission.InventoryView)]
```

When you need "all of these", always stack attributes — don't combine flags.

### Combining with `[RequireSuperAdminTenant]`

`[RequireSuperAdminTenant]` is a separate `IAuthorizationFilter` that returns `403` (`Tenant.SuperAdminRequired`) unless the user belongs to `TenantConstants.SuperAdminTenantId`. It does not look at permissions. `TenantsController` uses both, so a caller must be in the super admin tenant **and** hold the required permission:

```csharp
[RequireSuperAdminTenant]
public sealed class TenantsController(ISender sender) : ControllerBase
{
    [RequirePermission(Permission.AdministrationDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { ... }
}
```

## 7. Adding permissions for a new module

1. Add four members to `Permission` using the next free bits (currently 20–23):
   ```csharp
   // Quality (bits 20–23)
   QualityView   = 1L << 20,
   QualityCreate = 1L << 21,
   QualityEdit   = 1L << 22,
   QualityDelete = 1L << 23,
   ```
2. **Never renumber existing members.** Role values in the database and in issued JWTs are raw numbers — shifting a bit silently changes what every existing role grants.
3. Update role values in a **new** Reference seed script using `UPDATE "Roles" SET "Permissions" = ...` (e.g. `Administrator` becomes `0xFFFFFF` = 16777215). Don't edit `0001_roles.sql` — it has already run on existing databases (tracked in `seed_history`), and its `ON CONFLICT DO NOTHING` wouldn't overwrite existing rows anyway. See [database-seeding.md](../data/database-seeding.md).
4. Protect the new controller's actions with `[RequirePermission(...)]`.

## 8. Checking permissions outside attributes

If a handler needs to branch on a permission (rather than block the whole endpoint), inject `ICurrentUser` and use the same bitwise check:

```csharp
if ((currentUser.EffectivePermissions & Permission.FinanceEdit) != 0)
{
    // include editable fields
}
```

`Enum.HasFlag` also works (`currentUser.EffectivePermissions.HasFlag(Permission.FinanceEdit)`), but note that `HasFlag` requires **all** bits of the argument to be set (AND), whereas `& != 0` passes if **any** bit matches (OR). For single-permission checks they are equivalent.
