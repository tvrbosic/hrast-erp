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

# Apply EF Core migrations (before first run and after adding new migrations)
dotnet ef database update --project src/HrastERP.Infrastructure --startup-project src/HrastERP.API

# Run the API
dotnet run --project src/HrastERP.API/
```

## Architecture

**Modular monolith with vertical slice architecture** — single deployable unit, five independent business modules (Administration, Finance, Inventory, Procurement, Production). Each module is a **single project** with Clean Architecture layers as folders (Domain, Application, Infrastructure, Web). The API project is a pure composition root.

**Project naming convention:** `HrastERP.<Module>` (e.g. `HrastERP.Inventory`). Each module contains `Domain/`, `Application/`, `Infrastructure/`, and `Web/` folders.

**Dependency wiring:** `HrastERP.API` references all module projects. Each module exposes a `Add<Module>Module()` extension method that registers MediatR handlers, FluentValidation validators, EF Core configurations, and repositories. The API layer calls these and registers controller assemblies via `AddApplicationPart()`. Shared infrastructure is registered via a single `AddInfrastructure()` call, which delegates to focused extension classes: `PersistenceServiceExtensions`, `BehaviorServiceExtensions`, and `AuthenticationServiceExtensions`.

**CQRS:** MediatR with commands and queries organized by feature inside `Application/`. Structure: `Application/<Feature>/Commands/` and `Application/<Feature>/Queries/`. Handlers return `Result<T>`.

**Pipeline behaviors** (registered in `HrastERP.Infrastructure`), in execution order:
- `LoggingBehavior` — structured request/response logging with timing; outermost wrapper
- `TenantValidationBehavior` — short-circuits with `Result.Failure(Error.Forbidden("General.MissingTenant", ...))` when `ICurrentTenant.TenantId` is `Guid.Empty`; runs before input validation
- `ValidationBehavior` — runs FluentValidation validators; on failure groups violations by camelCase property name into a field-level error dictionary and returns `Result.Failure` with `Error.Validation("General.Validation", ..., fieldErrors)`

**Inter-module communication:** Domain events only (MediatR notifications). Modules never reference each other. Cross-module event contracts live in SharedKernel under `IntegrationEvents/`.

**Dependency rules:** Since layers are folders not projects, inward-only dependencies (Domain ← Application ← Infrastructure) are enforced by convention. Domain code must not import from Application, Infrastructure, or Web folders.

## SharedKernel

`HrastERP.SharedKernel` is referenced by all modules. It is pure C# with no framework dependencies (no EF Core, no MediatR).

Key types and their intended use:

- **`BaseEntity<TId>`** — base for all entities; implements `IAuditable` + `ISoftDeletable`; equality is by `Id`. All entities are automatically auditable and soft-deletable.
- **`AggregateRoot<TId>`** — extends `BaseEntity`, adds `AddDomainEvent` / `ClearDomainEvents`
- **`IAuditable`** — interface with audit trail properties (`CreatedAt`, `CreatedBy`, `UpdatedAt?`, `UpdatedBy?`); `CreatedBy`/`UpdatedBy` are `Guid` (UserId). Implemented by `BaseEntity`.
- **`ISoftDeletable`** — interface with soft-delete properties (`DeletedAt?`, `DeletedBy?`). Implemented by `BaseEntity`.
- **`ITenantEntity`** — domain marker interface for entities requiring row-level tenant isolation; exposes `Guid TenantId { get; }`. Entities opt in explicitly — `BaseEntity` does NOT implement it. `TenantId` is auto-populated by `TenantEntityInterceptor` on insert; never set it manually.
- **`IDomainEvent`** — marker interface for domain events; aggregates raise them, the application layer dispatches after commit
- **`ValueObject`** — equality by `GetEqualityComponents()`; use for `Money`, `Address`, etc.
- **`Result` / `Result<TValue>`** — all command and query handlers return these instead of throwing for expected failures; supports implicit conversion from `TValue` and `Error`
- **`Error`** — `record(string Code, string Message, ErrorType Type)`; code is dot-separated e.g. `"Order.NotFound"`; `ErrorType` enum: `Validation`, `NotFound`, `Forbidden`, `Conflict`, `Unexpected`; factory methods: `Error.NotFound`, `Error.Validation`, `Error.Forbidden`, `Error.Conflict`, `Error.Unexpected`. `Error.Validation` accepts an optional `validationErrors` (`IReadOnlyDictionary<string, string[]>?`) for field-level error detail; populated automatically by `ValidationBehavior`. Each module defines errors as `static readonly` constants in a `<Module>Errors` class. See `docs/error-handling.md` and `docs/api-responses.md`.
- **`PagedResult<T>`** — returned by all list queries; created via `PagedResult<T>.Create(...)`
- **`Permission`** — `[Flags] enum Permission : long` in `HrastERP.SharedKernel/Authorization/`; 20 CRUD permissions across 5 modules (bits 0–19). Use for authorization checks — always reference named enum members, never raw `long` values.
- **`ICurrentUser`** / **`ICurrentTenant`** — injected into application handlers; implemented in API layer from JWT claims. `ICurrentUser.EffectivePermissions` returns the current user's combined permissions (parsed from the `"permissions"` JWT claim).
- **Global exception middleware** (`GlobalExceptionMiddleware`, in `HrastERP.API/Middleware/`) — registered as the first middleware in `Program.cs`; catches any unhandled infrastructure/framework exception and returns 500 with `{ code: "General.Unexpected", message: "An unexpected error occurred." }`. Application-layer failures always use `Result.Failure` — the middleware is a safety net only, not the primary error path.
- **Model binding error factory** (`ModelBindingExtensions.ConfigureModelBindingErrorFormat()`, in `HrastERP.API/Extensions/`) — replaces ASP.NET Core's default `InvalidModelStateResponseFactory` so that model binding failures (malformed JSON, missing `[Required]` fields, type mismatches) return the same `ErrorResponse` shape as application validation: HTTP 422 with `{ code: "General.Validation", message: "...", errors: { ... } }` and camelCase field names.
- **API response envelope** (`ApiResponse<T>`, in `HrastERP.API/Models/`) — all successful responses from `ToActionResult()` are wrapped as `{ "data": T }`. For `PagedResult<T>`, the envelope includes a `"meta"` field with pagination info (`page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`). Error responses use `ErrorResponse` (same `Models/` folder) and are **not** enveloped — they remain flat `{ "code", "message", "errors?" }`. Both types are the only response shapes the API produces; all endpoints must use them consistently.

**Audit fields:** All entities get `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` auto-populated by `AuditableEntityInterceptor` in the Infrastructure layer. Uses `DateTime` (UTC) and `ICurrentUser.UserId` (`Guid`). Falls back to `Guid.Empty` when unauthenticated.

**Soft delete:** All entities get `DeletedAt`/`DeletedBy` auto-populated by `SoftDeleteInterceptor` when deleted. A global query filter hides soft-deleted entities by default.

**Tenant isolation:** Entities opt in to row-level tenant scoping by implementing `ITenantEntity`. `TenantEntityInterceptor` auto-populates `TenantId` from `ICurrentTenant` on insert and throws `InvalidOperationException` if `TenantId` is `Guid.Empty` — a programming error caught before reaching the database. A global query filter restricts all `ITenantEntity` queries to the current tenant. `TenantValidationBehavior` provides an early defense at the pipeline level, returning `Result.Failure(Error.Forbidden("General.MissingTenant", ...))` before any handler executes when tenant context is missing.

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
- **`ApplicationUser`** — extends `IdentityUser<Guid>` with `TenantId`, `FirstName`/`LastName`, `IsActive`, and a nullable `RoleId` FK + `Role` navigation (single role per user)
- **`RefreshToken`** — entity for refresh token rotation, linked to `ApplicationUser`
- **`IAuthService` / `AuthService`** — login, register, refresh, and logout flows using Identity + token service
- **`ITokenService` / `TokenService`** — generates JWT access tokens and refresh tokens
- **`AuthErrors`** — predefined `Error` constants for auth failures (e.g. `Auth.InvalidCredentials`)

**API layer** (`HrastERP.API/`):
- **`AuthController`** — endpoints: `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`
- **`CurrentUser`** / **`CurrentTenant`** — implement `ICurrentUser` / `ICurrentTenant` by reading JWT claims from `HttpContext`

**Configuration:** JWT settings are in `appsettings.json` under `"Jwt"` section (`SecretKey`, `Issuer`, `Audience`), bound to `JwtSettings` with validation on startup.

**DbContext:** `HrastDbContext` inherits from `IdentityUserContext<ApplicationUser, Guid>` (not plain `DbContext`), which adds Identity tables to the EF Core model.

## Authorization

Custom RBAC system built on ASP.NET Core policy-based authorization. Permissions are embedded in the JWT at login — no per-request DB query.

**How to protect an endpoint:**

```csharp
[RequirePermission(Permission.FinanceView)]
[HttpGet("invoices")]
public async Task<IActionResult> GetInvoices() { ... }
```

**Key types** (`HrastERP.API/Authorization/`):
- **`[RequirePermission]`** — `AuthorizeAttribute` subclass; encodes the permission as policy name `"Permission:<long>"`. Supports `AllowMultiple = true` for stacking multiple required permissions.
- **`PermissionPolicyProvider`** — `IAuthorizationPolicyProvider` that intercepts `"Permission:*"` policy names and builds them dynamically; delegates all other policies to the default provider. Registered as singleton.
- **`PermissionAuthorizationHandler`** — evaluates `(ICurrentUser.EffectivePermissions & requirement.Permission) != 0`. Registered as scoped (depends on scoped `ICurrentUser`).

**`Role` entity** (`HrastERP.Infrastructure/Authorization/`):
- Does NOT inherit `BaseEntity<TId>` — roles have no auditing, soft-delete, or tenant isolation
- `Permissions` stored as `bigint` via `HasConversion<long>()`
- Single role per user via nullable `ApplicationUser.RoleId` FK; `OnDelete(SetNull)` clears the FK on all users when a role is deleted
- See `docs/authorization.md` for the full reference

## Database Seeding

`DatabaseSeeder` (`src/HrastERP.Infrastructure/Persistence/DatabaseSeeder.cs`) runs at application startup via `Program.cs` before the middleware pipeline. It requires migrations to be applied first.

Plain SQL scripts live in two folders inside `src/HrastERP.Infrastructure/Seeds/`:

- `Reference/` — always applied in every environment
- `Fixtures/` — applied only in `Development` environment

**Script naming:** `NNNN_description.sql` (4-digit zero-padded prefix). Scripts execute in ascending numeric order within each folder; Reference scripts always run before Fixtures.

**Idempotency:** Applied scripts are recorded in `seed_history` (a plain table created by the seeder, not managed by EF Core migrations). Scripts themselves use `INSERT ... ON CONFLICT ("Id") DO NOTHING`. A script runs exactly once per database.

**Predefined role UUIDs** (stable across environments — used as FK targets):
- Administrator: `00000000-0000-0000-0000-000000000001`
- ProcurementOperator: `00000000-0000-0000-0000-000000000002`
- ProductionWorker: `00000000-0000-0000-0000-000000000003`
- WarehouseEmployee: `00000000-0000-0000-0000-000000000004`
- FinanceEmployee: `00000000-0000-0000-0000-000000000005`

See `docs/database-seeding.md` for the full guide on adding new seed scripts.

## Test stack

xUnit + FluentAssertions. Each module has a dedicated test project under `tests/`. `xunit` is a global using in test projects — no need to add `using Xunit;`.
