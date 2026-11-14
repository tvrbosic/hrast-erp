# Adding Entities

This document describes the steps required to add a new domain entity to a module and have it persisted via EF Core.

---

## 1. Define the entity (Domain layer)

First decide where it lives. Ask: *is it about who / for whom (identity or tenancy), and do several modules need to read it?* If yes, it belongs to the shared identity model in `HrastERP.Infrastructure/Identity/` (with tenants, users, roles and refresh tokens): every module may read it, only Administration changes it. Otherwise it belongs to its module, and other modules learn about it through events.

Create the entity class in the `Domain/Entities/` folder of the module project, extending the appropriate base class from `HrastERP.SharedKernel`:

| Base class | Use for |
|---|---|
| `BaseEntity<TId>` | Non-aggregate entities (e.g. `OrderLine`) |
| `AggregateRoot<TId>` | Aggregate roots with domain event support (e.g. `Order`, `Product`) |

Both base classes implement `IAuditable` and `ISoftDeletable`, so every entity automatically gets audit trail (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`) and soft-delete (`DeletedAt`, `DeletedBy`) fields. These are populated automatically by EF Core interceptors — do not set them in domain code.

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

To make an entity **tenant-scoped**, also implement `ITenantEntity` and add a `TenantId` property. See `docs/architecture/multi-tenancy.md` for the full guide. Note: `TenantEntityInterceptor` auto-populates `TenantId` only when it is `Guid.Empty` — if you set it explicitly in the handler (e.g. super admin creating for another tenant), the interceptor skips it.

---

## 2. Create the EF Core configuration (Infrastructure layer)

Create an `IEntityTypeConfiguration<TEntity>` class in the `Infrastructure/Configurations/` folder of the module project.

```csharp
// src/Modules/Inventory/HrastERP.Inventory/Infrastructure/Configurations/ProductConfiguration.cs
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", DatabaseSchemas.Inventory);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
    }
}
```

Always call `ToTable("<plural_snake>", DatabaseSchemas.<Module>)` — the table name must be plural snake_case and the schema is set by the rule below.

**Schema rule: a table's schema is the module whose business data it holds**, not the project that happens to contain its entity class:

- Module data → that module's schema (`inventory`, `finance`, …). This includes data whose entity class has to live in `HrastERP.Infrastructure` for technical reasons: the shared identity model — `tenants`, users (`users`, and Identity's `user_claims`, `user_logins`, `user_tokens`), `roles` and `refresh_tokens` — is needed by auth, JWT issuing, permission checks, tenant query filters and other modules, and Infrastructure cannot reference a module, so its classes live in `HrastERP.Infrastructure/Identity/` while its tables sit in `administration`.
- Technical tables no module owns → `infrastructure` (`audit_logs`, `stored_files`, `seed_history`, `ef_migrations_history`).
- Hangfire's tables → `hangfire` (configured explicitly; Hangfire creates and migrates them itself).
- Nothing lives in `public`.

Every configuration names its schema with a `DatabaseSchemas` constant (`HrastERP.Infrastructure.Database`), never a string literal: one per module (`Administration`, `Finance`, `Inventory`, `Procurement`, `Production`) plus `Infrastructure` and `Hangfire`. Living in Infrastructure, they are usable from both modules and Infrastructure without Infrastructure referencing a module. `DatabaseNamingConventionTests` guards the rule: it fails when an entity has no schema, a module entity isn't in its module's schema, or an Infrastructure entity isn't in `infrastructure` without being listed in its `ModuleOwnedInfrastructureEntities` map. The snake_case naming convention converts column, key and index names automatically, so never use `HasColumnName` or `HasDatabaseName` just to fix casing.

**No further registration is needed.** When the module's `Add<Module>Module()` is called at startup, it registers the module assembly as an `EntityConfigurationAssembly` singleton in DI. On first use, `HrastDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly` for every registered module assembly, which picks up all `IEntityTypeConfiguration<T>` implementations automatically.

---

## 3. Generate and apply a migration

```bash
# Add a migration (run from the solution root)
dotnet ef migrations add <MigrationName> --project src/HrastERP.Infrastructure --startup-project src/HrastERP.API

# Apply to the database
dotnet ef database update --project src/HrastERP.Infrastructure --startup-project src/HrastERP.API
```

---

## How assembly scanning works

`HrastDbContext` receives an `IEnumerable<EntityConfigurationAssembly>` from DI. Each module's `Add<Module>Module()` extension registers its own assembly into this collection. `OnModelCreating` iterates them all and scans each for `IEntityTypeConfiguration<T>` implementations.

Adding a new module follows the same pattern: call `services.AddSingleton(new EntityConfigurationAssembly(Assembly.GetExecutingAssembly()))` inside the new module's `Add<Module>Module` method. Nothing in the core infrastructure changes.
