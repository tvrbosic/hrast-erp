# hrast-erp
Hrast ERP is an imaginary practice project created for educational and portfolio purposes. The project is intended to demonstrate software engineering knowledge, backend architecture design, and implementation practices using ASP.NET and related technologies.

## Architecture

The solution follows a **modular monolith** architecture — a single deployable unit divided into independent business modules with clear boundaries. Each module internally follows **Clean Architecture** principles, with dependencies pointing inward: Infrastructure and Application depend on Domain, never the other way around.

Each business module is a **single project** (`HrastERP.<Module>`) with Clean Architecture layers organized as folders:

| Folder | Role |
|---|---|
| **Domain/** | Domain models, value objects, events, and repository interfaces |
| **Application/** | Use cases organized by feature (commands and queries via CQRS) |
| **Infrastructure/** | Persistence (EF Core configurations) and repository implementations |
| **Web/** | API controllers |

The API project (`HrastERP.API`) is a pure composition root — it references all modules and registers their services. A shared `HrastERP.SharedKernel` library provides common abstractions used across modules. A shared `HrastERP.Infrastructure` library provides the `HrastDbContext`, pipeline behaviors, and core infrastructure.

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
        Infrastructure/                      # Infrastructure layer (persistence, repositories)
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
