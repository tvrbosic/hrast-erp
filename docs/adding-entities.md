# Adding Entities

This document describes the steps required to add a new domain entity to a module and have it persisted via EF Core.

---

## 1. Define the entity (Domain layer)

Create the entity class in `HrastERP.<Module>.Domain`, extending either `BaseEntity<TId>` or `AggregateRoot<TId>` from `HrastERP.SharedKernel`:

- Use `BaseEntity<TId>` for plain entities that are not aggregate roots.
- Use `AggregateRoot<TId>` for aggregate roots that raise domain events.

```csharp
// src/Modules/Inventory/HrastERP.Inventory.Domain/Product.cs
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

---

## 2. Create the EF Core configuration (Infrastructure layer)

Create an `IEntityTypeConfiguration<TEntity>` class in `HrastERP.<Module>.Infrastructure`. Place it under a `Persistence/Configurations/` subfolder by convention.

```csharp
// src/Modules/Inventory/HrastERP.Inventory.Infrastructure/Persistence/Configurations/ProductConfiguration.cs
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "inventory");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
    }
}
```

**No further registration is needed.** When the module's `AddXxxInfrastructure()` is called at startup, it registers the module assembly as an `EntityConfigurationAssembly` singleton in DI. On first use, `HrastDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly` for every registered module assembly, which picks up all `IEntityTypeConfiguration<T>` implementations automatically.

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

`HrastDbContext` receives an `IEnumerable<EntityConfigurationAssembly>` from DI. Each module's `Add<Module>Infrastructure()` extension registers its own assembly into this collection. `OnModelCreating` iterates them all and scans each for `IEntityTypeConfiguration<T>` implementations.

Adding a new module follows the same pattern: call `services.AddSingleton(new EntityConfigurationAssembly(Assembly.GetExecutingAssembly()))` inside the new module's `Add<Module>Infrastructure` method. Nothing in the core infrastructure changes.
