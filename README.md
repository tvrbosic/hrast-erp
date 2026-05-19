# hrast-erp
Hrast ERP is an imaginary practice project created for educational and portfolio purposes. The project is intended to demonstrate software engineering knowledge, backend architecture design, and implementation practices using ASP.NET and related technologies.

## Architecture

The solution follows a **modular monolith** architecture — a single deployable unit divided into independent business modules with clear boundaries. Each module internally follows **Clean Architecture** principles, with dependencies pointing inward: Infrastructure and Application depend on Domain, never the other way around.

Each business module is structured as an independent vertical slice with four layers:

| Layer | Role |
|---|---|
| **Presentation (API)** | `HrastERP.API` — single ASP.NET Web API entry point shared across all modules |
| **Application** | `<Module>.Application` — use cases and application logic per module |
| **Domain** | `<Module>.Domain` — domain models and business rules per module |
| **Infrastructure** | `<Module>.Infrastructure` — persistence and external integrations per module |

A shared `HrastERP.SharedKernel` library provides common abstractions used across modules.

## Modules

- **Administration**
- **Finance**
- **Inventory**
- **Procurement**
- **Production**

## Project Structure

```
src/
  HrastERP.API/                              # Presentation layer (ASP.NET Web API)
  HrastERP.SharedKernel/                     # Shared abstractions and utilities
  Modules/
    <Module>/
      HrastERP.<Module>.Domain/              # Domain layer
      HrastERP.<Module>.Application/         # Application layer
      HrastERP.<Module>.Infrastructure/      # Infrastructure layer
tests/
  HrastERP.SharedKernel.Tests/
  <Module>.Tests/                            # Per-module test projects
```

## Technology

- .NET 10
- xUnit + FluentAssertions (testing)
