# Multi-Tenancy

This document explains how row-level tenant isolation works in HrastERP and how to make a domain entity tenant-scoped.

---

## Overview

HrastERP uses **row-level tenant isolation**: each tenant's data lives in the same database tables, distinguished by a `TenantId` column. Entities opt in to tenant scoping by implementing `ITenantEntity`. Entities that do not implement it are shared across all tenants (e.g. the `Tenant` entity itself, system-wide lookup tables).

The current tenant is resolved from the JWT claim on every request via `ICurrentTenant`, implemented by `CurrentTenant` in the API layer. The resolved `TenantId` flows through two enforcement layers:

| Layer | Mechanism | Failure mode |
|---|---|---|
| MediatR pipeline | `TenantValidationBehavior` | `Result.Failure(Error.Forbidden(...))` — before handler runs |
| EF Core | `TenantEntityInterceptor` + global query filter | `InvalidOperationException` on save; invisible rows on read |

---

## Making an entity tenant-scoped

### 1. Implement `ITenantEntity`

Add `ITenantEntity` to the entity class alongside the base class. Declare `TenantId` with a `private set` — EF Core can write through it, but domain code cannot:

```csharp
// src/Modules/Inventory/HrastERP.Inventory/Domain/Entities/Product.cs
public class Product : AggregateRoot<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    private Product() { } // required by EF Core

    public static Product Create(Guid id, string name)
    {
        return new Product { Id = id, Name = name };
        // Do NOT set TenantId here — TenantEntityInterceptor sets it automatically on save.
    }
}
```

That is the only change needed in domain code. Do not pass `TenantId` into factory methods or constructors.

### 2. Map `TenantId` in the EF Core configuration

Add the `TenantId` column to the entity's `IEntityTypeConfiguration<T>`:

```csharp
// src/Modules/Inventory/HrastERP.Inventory/Infrastructure/Database/Configurations/ProductConfiguration.cs
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "inventory");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
    }
}
```

### 3. Generate a migration

```bash
dotnet ef migrations add AddTenantIdToProducts \
    --project src/HrastERP.Infrastructure \
    --startup-project src/HrastERP.API
```

That is all. No other wiring is needed.

---

## What happens automatically

Once an entity implements `ITenantEntity`, the following behaviors activate without any additional code:

### On write — `TenantEntityInterceptor`

`TenantEntityInterceptor` runs on every `SaveChanges` / `SaveChangesAsync` call:

- **Added entities:** If the entity's `TenantId` is already set (not `Guid.Empty`), the interceptor skips it — this allows super admin users to create entities for other tenants by explicitly setting `TenantId` in the handler. If `TenantId` is `Guid.Empty`, it is populated from `ICurrentTenant.TenantId`. If `TenantId` is still `Guid.Empty` after this (no valid tenant in context), an `InvalidOperationException` is thrown — a programming error, not a user-facing failure.
- **Modified entities:** `TenantId` is never touched — tenant reassignment is not permitted.
- **Deleted entities:** Not relevant; soft-delete is handled by `SoftDeleteInterceptor`.

### On read — global query filter

`HrastDbContext` registers a global query filter for every entity type implementing `ITenantEntity`:

```
WHERE CurrentTenantId = SuperAdminTenantId OR TenantId = @currentTenantId
```

This filter is applied automatically to every LINQ query. For regular tenant users, cross-tenant rows are invisible. For super admin tenant users (`TenantConstants.SuperAdminTenantId`), the filter is bypassed and all tenant records are visible. You never need to add `.Where(x => x.TenantId == ...)` manually.

To bypass the filter explicitly (purge jobs, middleware lookups):

```csharp
await dbContext.Products.IgnoreQueryFilters().ToListAsync();
```

### On request — `TenantValidationBehavior`

Before any MediatR handler executes, `TenantValidationBehavior` checks that `ICurrentTenant.TenantId` is not `Guid.Empty`. If it is, the pipeline short-circuits:

```json
HTTP 403
{
    "code": "General.MissingTenant",
    "message": "A valid tenant context is required."
}
```

This catches missing tenant context at the application layer before any domain or database code runs.

---

## Entities that should NOT be tenant-scoped

Not all entities belong to a tenant. Examples:

- The `Tenant` entity itself — it is a system-wide record
- System-wide lookup tables (currencies, units of measure, countries)
- `RefreshToken` — linked to `ApplicationUser` via FK, not tenant-scoped directly

For these, simply do not implement `ITenantEntity`. No query filter will be applied, and `TenantEntityInterceptor` will ignore them.

Note: `ApplicationUser` **does** implement `ITenantEntity` — users are tenant-scoped. The super admin tenant bypass ensures admin users can still see and manage users across all tenants.

---

## Testing tenant-scoped entities

When writing unit or integration tests for tenant-scoped entities, supply a non-empty `TenantId` via a fake `ICurrentTenant`:

```csharp
private sealed class FakeCurrentTenant(Guid tenantId) : ICurrentTenant
{
    public Guid TenantId => tenantId;
}
```

Pass it to `TenantEntityInterceptor` when building a test `DbContext`:

```csharp
var tenantId = Guid.NewGuid();
var interceptor = new TenantEntityInterceptor(new FakeCurrentTenant(tenantId));

var options = new DbContextOptionsBuilder<TestDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .AddInterceptors(interceptor)
    .Options;
```

See `tests/HrastERP.Infrastructure.Tests/Database/TenantEntityInterceptorTests.cs` for complete examples.
