---
name: he-add-entity
description: Guides adding a domain entity to a HrastERP module — choosing BaseEntity vs AggregateRoot, EF Core configuration with schema namespacing, optional ITenantEntity opt-in, and generating a migration. Use when adding a new entity to any module.
---

# HrastERP: Adding a Domain Entity

---

## Step 1 — Define the entity (Domain layer)

First decide where it belongs with the "who / for whom" test in `docs/data/adding-entities.md` ("Where does the entity live?"). Shared identity model → `HrastERP.Infrastructure/Identity/` (configuration in `Identity/Persistence/`, table in `administration`). Otherwise → its module, as below.

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

Create `IEntityTypeConfiguration<TEntity>` in `Infrastructure/Configurations/`:

```csharp
// src/Modules/Inventory/HrastERP.Inventory/Infrastructure/Configurations/ProductConfiguration.cs
using HrastERP.Infrastructure.Database; // DatabaseSchemas
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", DatabaseSchemas.Inventory);  // plural snake_case table, schema = the module's DatabaseSchemas constant
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
| Using a flat table name without schema | Use `builder.ToTable("products", DatabaseSchemas.Inventory)` for module isolation |
| Omitting `ToTable` | Always call it; the snake_case convention does not pluralize or pick a schema, so the table would get the singular class name in Postgres's default `public` schema, which no table may use (`DatabaseNamingConventionTests` fails on any entity without a schema) |
| Putting module data in `infrastructure` because the entity class lives in `HrastERP.Infrastructure` | The schema is the module whose data it is; use the module's `DatabaseSchemas` constant and add the type to `ModuleOwnedInfrastructureEntities` in `DatabaseNamingConventionTests` |
| Hand-naming columns with `HasColumnName` / `HasDatabaseName` | Don't; `EFCore.NamingConventions` already produces snake_case columns, keys and indexes |
| Registering `IEntityTypeConfiguration<T>` manually | Not needed — assembly scanning handles it |
