# hrast-erp
Hrast ERP is an imaginary practice project created for educational and portfolio purposes. The project is intended to demonstrate software engineering knowledge, backend architecture design, and implementation practices using ASP.NET and related technologies.

## Architecture

The solution follows a **modular monolith** architecture — a single deployable unit divided into independent business modules with clear boundaries.

### Clean Architecture Layers

The codebase maps to Clean Architecture at two levels: **shared projects** provide cross-cutting concerns, while each **module** contains its own layers as folders.

| Clean Architecture Layer | Shared (project) | Per-module (folder) |
|---|---|---|
| **Domain** | `HrastERP.SharedKernel` — base entities, value objects, Result pattern, interfaces | `Domain/` — module-specific entities, value objects, events, repository interfaces |
| **Application** | — | `Application/` — use cases organized by feature (commands and queries via CQRS) |
| **Infrastructure** | `HrastERP.Infrastructure` — DbContext, interceptors, Identity, Hangfire, pipeline behaviors | `Infrastructure/` — EF Core configurations, repository implementations |
| **Presentation** | `HrastERP.API` — composition root, middleware, authorization | `Web/` — module-specific API controllers |

`HrastERP.SharedKernel` is the shared **Domain** layer — pure C# with no framework dependencies. It defines the abstractions (`BaseEntity`, `AggregateRoot`, `ValueObject`, `Result`, `Error`, `ICurrentUser`, `ICurrentTenant`) that all modules build on.

`HrastERP.Infrastructure` is the shared **Infrastructure** layer — it owns the single `HrastDbContext`, EF Core interceptors, ASP.NET Core Identity, Hangfire, and MediatR pipeline behaviors.

`HrastERP.API` is the **composition root** — it references all modules and shared infrastructure, wires everything together in `Program.cs`, and hosts the middleware pipeline. It contains no business logic.

### Project Dependencies

All dependencies point inward, following the Clean Architecture dependency rule:

```
HrastERP.API (composition root)
├── HrastERP.Infrastructure (shared infrastructure)
│   └── HrastERP.SharedKernel (shared domain)
└── HrastERP.<Module> (x5 — each business module)
    ├── HrastERP.Infrastructure
    └── HrastERP.SharedKernel
```

Modules never reference each other — cross-module communication uses domain events only.

## Modules

- **Administration**
- **Finance**
- **Inventory**
- **Procurement**
- **Production**

## Project Structure

```
src/
  HrastERP.API/                              # Composition root (ASP.NET Web API)
  HrastERP.SharedKernel/                     # Shared abstractions and utilities
  HrastERP.Infrastructure/                   # Core infrastructure (DbContext, pipeline behaviors)
  Modules/
    <Module>/
      HrastERP.<Module>/                     # Single project per module
        Domain/                              # Domain layer (entities, value objects, events, repositories)
        Application/                         # Application layer (features with commands/queries)
        Infrastructure/                      # Infrastructure layer (database, repositories)
        Web/                                 # Presentation layer (controllers)
tests/
  HrastERP.SharedKernel.Tests/
  HrastERP.Infrastructure.Tests/
  <Module>.Tests/                            # Per-module test projects
```

## Technology

- .NET 10
- PostgreSQL + EF Core (Npgsql)
- ASP.NET Core Identity + JWT Bearer authentication
- MediatR (CQRS + pipeline behaviors)
- FluentValidation
- xUnit + FluentAssertions (testing)

## Development Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for PostgreSQL)

### Steps

1. **Start the database**
   ```bash
   docker compose up -d
   ```

2. **Apply EF Core migrations**
   ```bash
   dotnet ef database update --project src/HrastERP.Infrastructure --startup-project src/HrastERP.API
   ```

3. **Run the application**
   ```bash
   dotnet run --project src/HrastERP.API
   ```
   Seed scripts apply automatically on startup. Reference data (roles) is seeded in all environments. Dev fixtures (admin user) are seeded only in the `Development` environment.

### Development Credentials

| Field | Value |
|---|---|
| Username | `admin` |
| Password | `admin` |

### Resetting the Database

To start fresh: stop the app, drop the `hrastErp` database, recreate it, re-apply migrations, and restart the app. Seed scripts will re-apply automatically.
