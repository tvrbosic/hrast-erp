# Modules

This document describes the modular monolith structure and the steps required to add a new business module to the application.

---

## Architecture Overview

The application is a modular monolith — a single deployable unit composed of independent business modules. Each module is a **single .NET project** with Clean Architecture layers organized as folders:

```
src/Modules/<Module>/HrastERP.<Module>/
├── Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   └── Enumerations/
├── Application/
│   └── <Feature>/
│       ├── Commands/
│       └── Queries/
├── Infrastructure/
│   └── Database/
│       └── Configurations/
└── <Module>Module.cs          # DI registration entry point
```

The API project (`HrastERP.API`) is the **composition root** — it references all modules, wires them together at startup, and **owns all controllers**. Controllers live in `HrastERP.API/Controllers/`, not in module projects. This avoids a circular dependency: modules reference Infrastructure and SharedKernel, while controllers need API-layer types (`ResultExtensions`, `RequirePermissionAttribute`, `ErrorResponse`) that live in the API project. If controllers were in modules, modules would need to reference the API project, creating a cycle (API → Module → API).

Modules never reference each other; cross-module communication uses domain events (MediatR notifications) with contracts defined in `HrastERP.SharedKernel/IntegrationEvents/`.

---

## Existing Modules

| Module | Project | Scope |
|---|---|---|
| Administration | `HrastERP.Administration` | Tenants, users, roles |
| Finance | `HrastERP.Finance` | Invoicing, payments |
| Inventory | `HrastERP.Inventory` | Products, stock |
| Procurement | `HrastERP.Procurement` | Purchasing, suppliers |
| Production | `HrastERP.Production` | Manufacturing, work orders |

---

## Adding a New Module

### 1. Create the project

Create a class library project under `src/Modules/<Module>/HrastERP.<Module>/` with the standard folder structure.

The `.csproj` needs these references:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
    <PackageReference Include="MediatR" Version="12.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\HrastERP.SharedKernel\HrastERP.SharedKernel.csproj" />
    <ProjectReference Include="..\..\..\HrastERP.Infrastructure\HrastERP.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- **`Microsoft.AspNetCore.App`** — framework reference needed for ASP.NET Core types used in module code
- **`FluentValidation.DependencyInjectionExtensions`** — enables `AddValidatorsFromAssembly` for automatic validator discovery
- **`MediatR`** — enables `AddMediatR` for automatic handler discovery
- **`HrastERP.SharedKernel`** — base classes (`BaseEntity`, `AggregateRoot`, `Result<T>`, etc.)
- **`HrastERP.Infrastructure`** — `EntityConfigurationAssembly`, `HrastDbContext`, and shared infrastructure services

### 2. Create the module entry point

Every module exposes a single `Add<Module>Module()` extension method that registers all of the module's services with DI. Create `<Module>Module.cs` at the project root:

```csharp
using FluentValidation;
using HrastERP.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.<Module>;

public static class <Module>Module
{
    public static IServiceCollection Add<Module>Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(<Module>Module).Assembly;

        // Registers all MediatR command and query handlers defined in this module's assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Registers all FluentValidation validators defined in this module's assembly
        services.AddValidatorsFromAssembly(assembly);

        // Registers this module's assembly so HrastDbContext can discover and apply EF Core entity configurations
        services.AddSingleton(new EntityConfigurationAssembly(assembly));

        return services;
    }
}
```

As the module grows, register additional services (repositories, module-specific services) in this method.

### 3. Wire it up in Program.cs

Two registrations are needed in `src/HrastERP.API/Program.cs`:

**a) Register module services** — add the `Add<Module>Module` call alongside the existing modules:

```csharp
builder.Services
    .AddAdministrationModule(builder.Configuration)
    .AddFinanceModule(builder.Configuration)
    // ...existing modules...
    .Add<Module>Module(builder.Configuration);
```

**b) Add controllers** — create controller classes in `src/HrastERP.API/Controllers/` for the module's endpoints. Controllers are in the API project (not in the module) to avoid circular dependencies. They reference the module's commands, queries, and DTOs via the API project's reference to the module.

### 4. Add a project reference from the API

Add a `<ProjectReference>` in `src/HrastERP.API/HrastERP.API.csproj`:

```xml
<ProjectReference Include="..\Modules\<Module>\HrastERP.<Module>\HrastERP.<Module>.csproj" />
```

---

## How Assembly Scanning Works

The three `Add*` calls in `<Module>Module.cs` enable automatic discovery of handlers, validators, and EF Core configurations from the module assembly. No manual registration of individual classes is needed.

### MediatR handlers

`AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly))` scans the module assembly for all classes implementing `IRequestHandler<TRequest, TResponse>` and `INotificationHandler<TNotification>`, and registers them in DI. Any command/query handler or domain event handler placed in the `Application/` folder is picked up automatically.

### FluentValidation validators

`AddValidatorsFromAssembly(assembly)` scans the module assembly for all classes extending `AbstractValidator<T>` and registers them as `IValidator<T>` in DI. The `ValidationBehavior` pipeline behavior (registered globally in `HrastERP.Infrastructure`) resolves all `IValidator<TRequest>` for each MediatR request and runs them before the handler executes. A validator placed next to its command (e.g. `Application/Tenants/Commands/CreateTenant/CreateTenantCommandValidator.cs`) is discovered and wired automatically.

### EF Core entity configurations

`services.AddSingleton(new EntityConfigurationAssembly(assembly))` registers the module's assembly reference in DI. `HrastDbContext` receives all registered `EntityConfigurationAssembly` instances via constructor injection and calls `ApplyConfigurationsFromAssembly` for each one during `OnModelCreating`. Any `IEntityTypeConfiguration<TEntity>` class placed in the module's `Infrastructure/Database/Configurations/` folder is applied automatically. See `docs/adding-entities.md` for details on adding entities.

### Controllers

Controllers live in `HrastERP.API/Controllers/`, not in module projects. They are discovered automatically by MVC from the API assembly. Each controller uses `ISender` (MediatR) to dispatch commands/queries defined in the module's `Application/` layer, and `ResultExtensions.ToActionResult()` to map results to HTTP responses. This keeps modules free of API-layer dependencies.

---

## Module Isolation Rules

- **No cross-module references.** Modules must not reference each other's projects. If module A needs to react to something in module B, use domain events via MediatR notifications.
- **Inward-only dependencies.** Since layers are folders (not projects), the rule is enforced by convention: `Domain` must not import from `Application`, `Infrastructure`, or `Web`. `Application` must not import from `Infrastructure` or `Web`.
- **Shared code goes in SharedKernel.** Base classes, interfaces, value objects, and integration event contracts live in `HrastERP.SharedKernel`, which is referenced by all modules.
- **One DbContext.** All modules share `HrastDbContext`. Entity isolation is achieved through EF Core schema namespacing (e.g. `builder.ToTable("products", "inventory")`), not separate contexts.
