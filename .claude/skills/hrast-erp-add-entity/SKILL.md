---
name: hrast-erp-add-entity
description: Guides adding a domain entity to a HrastERP module — choosing BaseEntity vs AggregateRoot, EF Core configuration with schema namespacing, optional ITenantEntity opt-in, and generating a migration. Use when adding a new entity to any module.
---

# HrastERP: Adding a Domain Entity

---

## Step 1 — Define the entity (Domain layer)

Create the entity in `Domain/Entities/` of the module project.

| Base class | Use for |
|---|---|
| `BaseEntity<TId>` | Non-aggregate entities (e.g. `OrderLine`) |
| `AggregateRoot<TId>` | Aggregate roots with domain event support (e.g. `Order`, `Product`) |

Both base classes automatically provide audit trail (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`) and soft-delete (`DeletedAt`, `DeletedBy`) via EF Core interceptors — **do not set these in domain code**.

```csharp
// src/Modules/Inventory/HrastERP.Inventory/Domain/Entities/Product.cs
public class Product : AggregateRoot<Guid>
{
    public string Name { get; private set; }

    private Product() { } // required by EF Core

    public static Product Create(Guid id, string name)
    {
        var product = new Product { Id = id, Name = name };
        product.AddDomainEvent(new ProductCreatedEvent(id));
        return product;
    }
}
```

### Tenant-scoped entities

If the entity should be row-level isolated per tenant, also implement `ITenantEntity`:

```csharp
public class Product : AggregateRoot<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    // ...
    public static Product Create(Guid id, string name)
    {
        return new Product { Id = id, Name = name };
        // Do NOT set TenantId — TenantEntityInterceptor sets it automatically on save
    }
}
```

Do **not** implement `ITenantEntity` on: the `Tenant` entity itself, or system-wide lookup tables.

---

## Step 2 — Create EF Core configuration (Infrastructure layer)

Create `IEntityTypeConfiguration<TEntity>` in `Infrastructure/Database/Configurations/`:

```csharp
// src/Modules/Inventory/HrastERP.Inventory/Infrastructure/Database/Configurations/ProductConfiguration.cs
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "inventory");  // schema = module name, lowercase
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);

        // If ITenantEntity:
        // builder.Property(p => p.TenantId).IsRequired();
    }
}
```

No additional registration is needed — `Add<Module>Module()` already registered the module assembly, so `HrastDbContext` discovers this configuration automatically via `ApplyConfigurationsFromAssembly`.

---

## Step 3 — Generate and apply migration

```bash
# Add migration (run from solution root)
dotnet ef migrations add <MigrationName> \
    --project src/HrastERP.Infrastructure \
    --startup-project src/HrastERP.API

# Apply to database
dotnet ef database update \
    --project src/HrastERP.Infrastructure \
    --startup-project src/HrastERP.API
```

---

## Common Mistakes

| Mistake | Correct |
|---|---|
| Setting `CreatedAt`, `DeletedAt` in domain code | Let interceptors handle audit/soft-delete fields |
| Setting `TenantId` in factory method | Let `TenantEntityInterceptor` set it; or set it explicitly in handler for super admin cross-tenant creation |
| Forgetting `private Entity() {}` | EF Core requires a parameterless constructor |
| Using a flat table name without schema | Use `builder.ToTable("products", "inventory")` for module isolation |
| Registering `IEntityTypeConfiguration<T>` manually | Not needed — assembly scanning handles it |
