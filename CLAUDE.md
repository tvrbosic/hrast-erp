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

**Modular monolith** — single deployable unit, five independent business modules (Administration, Finance, Inventory, Procurement, Production). Each module follows **Clean Architecture** with inward-only dependencies: Infrastructure → Application → Domain. Domain never imports Application or Infrastructure.

**Project naming convention:** `HrastERP.<Module>.<Layer>` (e.g. `HrastERP.Inventory.Domain`).

**Dependency wiring:** `HrastERP.API` references only the Infrastructure project of each module. The API layer is the composition root.

## SharedKernel

`HrastERP.SharedKernel` is referenced by all layers of all modules. It is pure C# with no framework dependencies (no EF Core, no MediatR).

Key types and their intended use:

- **`BaseEntity<TId>`** — base for all entities; equality is by `Id`
- **`AggregateRoot<TId>`** — extends `BaseEntity`, adds `AddDomainEvent` / `ClearDomainEvents`
- **`IAuditable`** — interface for entities with audit trail (`CreatedAt`, `CreatedBy`, `UpdatedAt?`, `UpdatedBy?`); `CreatedBy`/`UpdatedBy` are `Guid` (UserId)
- **`AuditableEntity<TId>`** — extends `BaseEntity<TId>` + `IAuditable`; use for auditable non-aggregate entities
- **`AuditableAggregateRoot<TId>`** — extends `AggregateRoot<TId>` + `IAuditable`; use for auditable aggregate roots
- **`IDomainEvent`** — marker interface for domain events; aggregates raise them, the application layer dispatches after commit
- **`ValueObject`** — equality by `GetEqualityComponents()`; use for `Money`, `Address`, etc.
- **`Result` / `Result<TValue>`** — all command and query handlers return these instead of throwing for expected failures; supports implicit conversion from `TValue` and `Error`
- **`Error`** — `record(string Code, string Message)`; code is dot-separated e.g. `"Order.NotFound"`; factory methods: `Error.NotFound`, `Error.Validation`, `Error.Forbidden`
- **`PagedResult<T>`** — returned by all list queries; created via `PagedResult<T>.Create(...)`
- **`ICurrentUser`** / **`ICurrentTenant`** — injected into application handlers; implemented in API layer from JWT claims
- **Exceptions** (`NotFoundException`, `ForbiddenException`, `ValidationException`) — thrown by application layer, caught by global exception middleware in API which maps them to 404 / 403 / 422

**Audit fields:** Entities inheriting `AuditableEntity` or `AuditableAggregateRoot` get `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` auto-populated by `AuditableEntityInterceptor` in the Infrastructure layer. Uses `DateTime` (UTC) and `ICurrentUser.UserId` (`Guid`). Falls back to `Guid.Empty` when unauthenticated.

Nothing in SharedKernel should import from any module.

## Test stack

xUnit + FluentAssertions. Each module has a dedicated test project under `tests/`. `xunit` is a global using in test projects — no need to add `using Xunit;`.
