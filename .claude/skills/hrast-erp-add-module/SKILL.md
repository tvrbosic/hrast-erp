---
name: hrast-erp-add-module
description: Scaffolds a new HrastERP business module — project structure, csproj references, Add<Module>Module() DI entry point, assembly scanning, Program.cs wiring, and API controller placement. Use when adding a new module to the application.
---

# HrastERP: Adding a New Business Module

## Overview

HrastERP is a modular monolith. Each module is a single .NET class library with Clean Architecture layers as folders. Controllers always live in the API project, not the module.

---

## Step 1 — Create the project

Create `src/Modules/<Module>/HrastERP.<Module>/` with the standard folder structure:

```
HrastERP.<Module>/
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
└── <Module>Module.cs
```

`HrastERP.<Module>.csproj`:

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

---

## Step 2 — Create the module entry point

`<Module>Module.cs` at project root:

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

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddSingleton(new EntityConfigurationAssembly(assembly));

        return services;
    }
}
```

These three calls enable automatic discovery of all handlers, validators, and EF Core configurations from the module assembly — no manual per-class registration needed.

---

## Step 3 — Wire in Program.cs

In `src/HrastERP.API/Program.cs`, add the module registration alongside existing modules:

```csharp
builder.Services
    .AddAdministrationModule(builder.Configuration)
    .AddFinanceModule(builder.Configuration)
    // ... existing modules ...
    .Add<Module>Module(builder.Configuration);
```

Controllers for the new module go in `src/HrastERP.API/Controllers/` — not in the module project. See Step 4. Controllers are discovered automatically by MVC from the API assembly.

---

## Step 4 — Add project reference from API

In `src/HrastERP.API/HrastERP.API.csproj`:

```xml
<ProjectReference Include="..\Modules\<Module>\HrastERP.<Module>\HrastERP.<Module>.csproj" />
```

---

## Module Isolation Rules

- **No cross-module references.** Use domain events (MediatR notifications) via `HrastERP.SharedKernel/IntegrationEvents/` for cross-module communication.
- **Inward-only dependencies.** Domain must not import from Application or Infrastructure. Application must not import from Infrastructure.
- **Shared code → SharedKernel.** Base classes, interfaces, value objects, integration event contracts.
- **One DbContext.** All modules share `HrastDbContext`. Isolate tables with schema namespacing: `builder.ToTable("products", "inventory")`.
- **Controllers in API.** Never put controllers in module projects — this would create a circular dependency (API → Module → API).
