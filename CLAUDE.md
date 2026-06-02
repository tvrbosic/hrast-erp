# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run tests for a specific project
dotnet test tests/HrastERP.SharedKernel.Tests/

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run the API
dotnet run --project src/HrastERP.API/
```

## Architecture

**Modular monolith with vertical slice architecture** — single deployable unit, five independent business modules (Administration, Finance, Inventory, Procurement, Production). Each module is a **single project** with Clean Architecture layers as folders (Domain, Application, Infrastructure, Web). The API project is a pure composition root.

**Project naming convention:** `HrastERP.<Module>` (e.g. `HrastERP.Inventory`). Each module contains `Domain/`, `Application/`, `Infrastructure/`, and `Web/` folders.

**Dependency wiring:** `HrastERP.API` references all module projects. Each module exposes a `Add<Module>Module()` extension method that registers MediatR handlers, FluentValidation validators, EF Core configurations, and repositories. The API layer calls these and registers controller assemblies via `AddApplicationPart()`. Shared infrastructure is registered via a single `AddInfrastructure()` call, which delegates to focused extension classes: `PersistenceServiceExtensions`, `BehaviorServiceExtensions`, and `AuthenticationServiceExtensions`.

**CQRS:** MediatR with commands and queries organized by feature inside `Application/`. Structure: `Application/<Feature>/Commands/` and `Application/<Feature>/Queries/`. Handlers return `Result<T>`.

**Pipeline behaviors** (registered in `HrastERP.Infrastructure`):
- `ValidationBehavior` — runs FluentValidation validators, returns `Result.Failure` on validation errors
- `LoggingBehavior` — structured request/response logging with timing

**Inter-module communication:** Domain events only (MediatR notifications). Modules never reference each other. Cross-module event contracts live in SharedKernel under `IntegrationEvents/`.

**Dependency rules:** Since layers are folders not projects, inward-only dependencies (Domain ← Application ← Infrastructure) are enforced by convention. Domain code must not import from Application, Infrastructure, or Web folders.

## SharedKernel

`HrastERP.SharedKernel` is referenced by all modules. It is pure C# with no framework dependencies (no EF Core, no MediatR).

Key types and their intended use:

- **`BaseEntity<TId>`** — base for all entities; implements `IAuditable` + `ISoftDeletable`; equality is by `Id`. All entities are automatically auditable and soft-deletable.
- **`AggregateRoot<TId>`** — extends `BaseEntity`, adds `AddDomainEvent` / `ClearDomainEvents`
- **`IAuditable`** — interface with audit trail properties (`CreatedAt`, `CreatedBy`, `UpdatedAt?`, `UpdatedBy?`); `CreatedBy`/`UpdatedBy` are `Guid` (UserId). Implemented by `BaseEntity`.
- **`ISoftDeletable`** — interface with soft-delete properties (`DeletedAt?`, `DeletedBy?`). Implemented by `BaseEntity`.
- **`IDomainEvent`** — marker interface for domain events; aggregates raise them, the application layer dispatches after commit
- **`ValueObject`** — equality by `GetEqualityComponents()`; use for `Money`, `Address`, etc.
- **`Result` / `Result<TValue>`** — all command and query handlers return these instead of throwing for expected failures; supports implicit conversion from `TValue` and `Error`
- **`Error`** — `record(string Code, string Message)`; code is dot-separated e.g. `"Order.NotFound"`; factory methods: `Error.NotFound`, `Error.Validation`, `Error.Forbidden`
- **`PagedResult<T>`** — returned by all list queries; created via `PagedResult<T>.Create(...)`
- **`ICurrentUser`** / **`ICurrentTenant`** — injected into application handlers; implemented in API layer from JWT claims
- **Exceptions** (`NotFoundException`, `ForbiddenException`, `ValidationException`) — thrown by application layer, caught by global exception middleware in API which maps them to 404 / 403 / 422

**Audit fields:** All entities get `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` auto-populated by `AuditableEntityInterceptor` in the Infrastructure layer. Uses `DateTime` (UTC) and `ICurrentUser.UserId` (`Guid`). Falls back to `Guid.Empty` when unauthenticated.

**Soft delete:** All entities get `DeletedAt`/`DeletedBy` auto-populated by `SoftDeleteInterceptor` when deleted. A global query filter hides soft-deleted entities by default.

Nothing in SharedKernel should import from any module.

## Module structure

Each module follows this folder layout:

```
HrastERP.<Module>/
├── Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   ├── Enumerations/
│   └── Repositories/          # Interfaces only
├── Application/
│   └── <Feature>/
│       ├── Commands/
│       └── Queries/
├── Infrastructure/
│   ├── Persistence/
│   │   └── Configurations/
│   └── Repositories/
├── Web/
│   └── Controllers/
└── <Module>Module.cs          # DI registration entry point
```

## Authentication

JWT Bearer authentication with ASP.NET Core Identity. Key components:

**Infrastructure layer** (`HrastERP.Infrastructure/Authentication/`):
- **`ApplicationUser`** — extends `IdentityUser<Guid>` with `TenantId` and `FirstName`/`LastName`
- **`RefreshToken`** — entity for refresh token rotation, linked to `ApplicationUser`
- **`IAuthService` / `AuthService`** — login, register, refresh, and logout flows using Identity + token service
- **`ITokenService` / `TokenService`** — generates JWT access tokens and refresh tokens
- **`AuthErrors`** — predefined `Error` constants for auth failures (e.g. `Auth.InvalidCredentials`)

**API layer** (`HrastERP.API/`):
- **`AuthController`** — endpoints: `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`
- **`CurrentUser`** / **`CurrentTenant`** — implement `ICurrentUser` / `ICurrentTenant` by reading JWT claims from `HttpContext`

**Configuration:** JWT settings are in `appsettings.json` under `"Jwt"` section (`SecretKey`, `Issuer`, `Audience`), bound to `JwtSettings` with validation on startup.

**DbContext:** `HrastDbContext` inherits from `IdentityUserContext<ApplicationUser, Guid>` (not plain `DbContext`), which adds Identity tables to the EF Core model.

## Test stack

xUnit + FluentAssertions. Each module has a dedicated test project under `tests/`. `xunit` is a global using in test projects — no need to add `using Xunit;`.
