# AGENTS.md

This file provides guidance to AI coding agents (Claude Code, Cursor, Copilot, etc.) when working with code in this repository. Detailed guides live in `docs/`; each section below points to the one to read before working in that area.

## Executing an implementation plan

When working from an agreed implementation plan, work through it autonomously, but **stop and ask before deviating from it**. Do not decide on your own and report afterwards. Ask first when something comes up that:

- needs an architectural decision, or does not match the plan (a step that turns out wrong, incomplete, or in conflict with the existing code)
- moves, renames, or restructures files or folders in ways the plan does not explicitly list
- changes core architectural files not named in the current plan step: `Program.cs`, `HrastDbContext`, entity configurations, migrations, seed scripts, `AddInfrastructure()` / `*ServiceExtensions`, `<Module>Module.cs`, pipeline behaviors, middleware, authorization/authentication setup, SharedKernel types, `*.csproj` / `Directory.*.props`
- introduces a new architectural pattern, framework, or third-party package (or a major version bump) not agreed on earlier in the conversation

When asking, describe what you found, give the options with trade-offs, and recommend one. Keep working on parts of the plan that don't depend on the answer. Small local choices inside a planned step (naming a private helper, picking a test case) don't need approval.

## Commands

```bash
dotnet build
dotnet test                                        # all tests, requires Docker
dotnet test --filter "Category!=Integration"       # fast, no Docker required
dotnet test --filter "Category=Integration"        # integration only
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Apply EF Core migrations (before first run and after adding new migrations)
dotnet ef database update --project src/HrastERP.Infrastructure --startup-project src/HrastERP.API

dotnet run --project src/HrastERP.API/
```

## Architecture

**Modular monolith with vertical slice architecture** — single deployable unit, five independent business modules (Administration, Finance, Inventory, Procurement, Production). Each module is a **single project** (`HrastERP.<Module>`) with Clean Architecture layers as folders (`Domain/`, `Application/`, `Infrastructure/`) and a `<Module>Module.cs` exposing `Add<Module>Module()`, which registers MediatR handlers, FluentValidation validators, and EF Core configurations. See `docs/architecture/modules.md`.

**Controllers live in `HrastERP.API/Controllers/`**, not in modules. This avoids a circular dependency (API → Module → API for shared Web types like `ResultExtensions`, `RequirePermissionAttribute`, `ErrorResponse`).

**Infrastructure wiring:** shared infrastructure is registered via a single `AddInfrastructure()` call that delegates to `*ServiceExtensions` classes colocated with each concern. Exception: `LoggingServiceExtensions` is called directly on `WebApplicationBuilder` in `Program.cs` because Serilog's `UseSerilog()` needs `builder.Host`.

**CQRS:** MediatR, organized as `Application/<Feature>/Commands/` and `Application/<Feature>/Queries/`. Handlers return `Result<T>`. See `docs/architecture/mediatr-pipeline-behaviors.md`.

**Data access:** Handlers inject `HrastDbContext` directly — no repository pattern. With CQRS, each handler has a single focused query or command; repositories would be pass-through wrappers. Business rules like "name must be unique" are checked with a query in the handler before saving; the matching DB constraint (unique index, Restrict FK) is only a safety net, and handlers don't catch `DbUpdateException` — a rare race past the pre-check surfaces as a 500. Delete handlers follow the same pattern: they count every kind of record that still refers to the entity (whether or not an FK links them) and refuse with a `Conflict` error naming the dependents and their count (e.g. `Role.InUse`, `Tenant.HasUsers`) — never cascading or reassigning. A new entity that refers to an existing one adds its check to that entity's delete handler. See `docs/api/error-handling.md`.

**Database naming:** all tables and columns are snake_case, applied by `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`) in `HrastDbContext.OnConfiguring`, so every context construction path (API, tests, `dotnet ef`) gets it. The convention only handles casing, so every entity configuration must call `ToTable("<plural_snake>"[, "<module>"])`. Module tables go in a schema named after the module (e.g. `administration.tenants`); shared infrastructure tables stay in `public`. Never use `HasColumnName` / `HasDatabaseName` just to fix casing. The one exception is Identity: it sets its helper table names and its `EmailIndex` / `UserNameIndex` index names explicitly, which the convention doesn't rewrite, so they are overridden with snake_case names in `HrastDbContext.OnModelCreating` and `ApplicationUserConfiguration`. Raw SQL uses unquoted snake_case identifiers and schema-qualifies module tables. Audit log JSON keys are database column names. `seed_history` and Hangfire tables are not EF-managed and are unchanged. See `docs/data/adding-entities.md`.

**Pipeline behaviors** (registered in `HrastERP.Infrastructure`), in execution order:
- `LoggingBehavior` — outermost; logs requests with timing; properties marked `[SensitiveData]` are masked as `"***"`
- `TenantValidationBehavior` — short-circuits with `Error.Forbidden("General.MissingTenant", ...)` when `ICurrentTenant.TenantId` is `Guid.Empty`; runs before input validation
- `ValidationBehavior` — runs FluentValidation validators and returns `Error.Validation("General.Validation", ..., fieldErrors)` keyed by camelCase property name. Validators are `internal sealed` classes placed next to their command and are picked up by `AddValidatorsFromAssembly(assembly, includeInternalTypes: true)` in `Add<Module>Module()`; without that flag FluentValidation silently skips internal validators.

**Inter-module communication:** Domain events only (MediatR notifications). Modules never reference each other. Cross-module event contracts live in SharedKernel under `IntegrationEvents/`.

**Dependency rules:** Since layers are folders not projects, inward-only dependencies (Domain ← Application ← Infrastructure) are enforced by convention. Domain code must not import from Application or Infrastructure folders.

## SharedKernel

`HrastERP.SharedKernel` is pure C# with no framework dependencies (no EF Core, no MediatR) and must not import from any module. See `docs/architecture/shared-kernel.md`. Rules to follow:

- All entities inherit `BaseEntity<TId>` (or `AggregateRoot<TId>` to raise domain events), which makes them auditable and soft-deletable. Tenant scoping is opt-in: implement `ITenantEntity` — `BaseEntity` does NOT implement it.
- Handlers return `Result` / `Result<T>` for expected failures instead of throwing. Each module defines its errors as `static readonly` constants in a `<Module>Errors` class, using the `Error.*` factory methods. See `docs/api/error-handling.md`.
- List queries return `PagedResult<T>` created via `PagedResult<T>.Create(...)`.
- Authorization uses the `[Flags] enum Permission : long` — always reference named enum members, never raw `long` values.
- Mark sensitive MediatR request properties (passwords, tokens) with `[property: SensitiveData]` on record constructor parameters so `LoggingBehavior` masks them.
- `ICurrentUser` / `ICurrentTenant` (ambient request context) belong in SharedKernel; infrastructure service interfaces (email, file storage, jobs) belong in `HrastERP.Infrastructure`.
- `TenantConstants.SuperAdminTenantId` (`00000000-0000-0000-0000-100000000000`): users of this tenant bypass tenant query filters; their role permissions are still enforced.

**API responses:** successful responses from `ToActionResult()` are wrapped as `{ "data": T }` (`ApiResponse<T>`), plus a `"meta"` pagination field for `PagedResult<T>`. Errors use `ErrorResponse` and are **not** enveloped — flat `{ "code", "message", "errors?" }`. These are the only response shapes the API produces; all endpoints must use them. Model binding failures return the same shape (422 `General.Validation`), and `GlobalExceptionMiddleware` turns unhandled exceptions into 500 `General.Unexpected` — a safety net only, never the primary error path. See `docs/api/api-responses.md`.

**Automatic entity behavior** (EF Core interceptors in Infrastructure):
- Audit fields (`CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy`, UTC + `ICurrentUser.UserId`) and soft delete (`DeletedAt`/`DeletedBy`, hidden by a global query filter) are populated automatically.
- `TenantEntityInterceptor` sets `TenantId` on insert only when it is `Guid.Empty` (an explicitly set value is kept, so super admin can create entities for other tenants), and throws `InvalidOperationException` if it is still empty. A global query filter limits `ITenantEntity` queries to the current tenant, bypassed for the super admin tenant. See `docs/architecture/multi-tenancy.md`.
- `AuditLogInterceptor` runs last and writes every create/update/soft-delete to the append-only `audit_logs` table. See `docs/data/audit-log.md`.

## Administration Module

First business module — manages tenants and users.

**Tenant Management:** `Tenant` entity stored in `administration.tenants`. Full CRUD at `api/admin/tenants`, protected by `[RequireSuperAdminTenant]` + `[RequirePermission]`. Tenant inherits `BaseEntity<Guid>` but does NOT implement `ITenantEntity` — tenants don't belong to other tenants.

**User Management:** Operates directly on `ApplicationUser` (no separate User entity). Uses `UserManager` for identity operations (create, password), `HrastDbContext` for reads and field updates. Endpoints at `api/admin/users`, protected by `[RequirePermission]`. Super admin tenant users manage all tenants' users; regular tenant admins manage only their own tenant's users (via automatic tenant query filter on `ApplicationUser`).

**Role Management:** CQRS slice in `Administration/Application/Roles/` operating on the global `Role` entity (Infrastructure) via `HrastDbContext.Set<Role>()`. Endpoints at `api/admin/roles` (CRUD) and `api/admin/permissions` (all permissions grouped by module). Reads need `AdministrationView`; writes additionally need `[RequireSuperAdminTenant]`, applied per write action (not on the controller) so tenant admins can still read roles. Permissions travel over the API as enum names and are converted to/from the bitmask by `PermissionMapper`. Role names are trimmed and unique case-insensitively, enforced by a unique index on `Role.NormalizedName` (`Name.Trim().ToUpperInvariant()`). Outside the super admin tenant, users can only be given roles within the caller's own permissions (`RoleAssignment` in `Application/Users/`). The Administrator role (`RoleConstants.AdministratorRoleId`) cannot be deleted and must keep every permission. Role changes reach users on their next token refresh.

**Block Inactive Entity Middleware:** `BlockInactiveEntityMiddleware` runs after authentication, before authorization. Blocks requests from users with inactive accounts or belonging to inactive tenants. Uses `ICacheService` for cached status lookups (5-min TTL). Super admin tenant users are never blocked.

## Authentication & Authorization

JWT Bearer authentication with ASP.NET Core Identity; `HrastDbContext` inherits `IdentityUserContext<ApplicationUser, Guid>`. `ApplicationUser` implements `ITenantEntity` and has a single nullable `RoleId`. See `docs/security/jwt-authentication.md`.

- `AuthService` queries all use `IgnoreQueryFilters()` because auth flows run without a JWT (no tenant context), so tenant and soft-delete filters would otherwise hide all records. `IsActive` is checked explicitly after lookup.
- `RefreshToken.TenantId` is set explicitly from `user.TenantId` in `AuthService`, not by the interceptor, since login has no JWT context.

Custom RBAC: permissions are embedded in the JWT at login — no per-request DB query. Protect endpoints like this:

```csharp
[RequirePermission(Permission.FinanceView)]
[HttpGet("invoices")]
public async Task<IActionResult> GetInvoices() { ... }
```

`[RequirePermission]` supports stacking; `[RequireSuperAdminTenant]` restricts to the super admin tenant. `Role` does NOT inherit `BaseEntity<TId>` (no auditing, soft delete, or tenant isolation); deleting a role still assigned to a user is refused with `409 Role.InUse`. See `docs/security/authorization.md` and `docs/security/permission-checks-guide.md`.

## Database Seeding

`DatabaseSeeder` runs plain SQL scripts at startup (after migrations) from `src/HrastERP.Infrastructure/Database/Seeds/`: `Reference/` always, `Fixtures/` in `Development` only. Scripts are named `NNNN_description.sql`, run once per database (tracked in `seed_history`), and use `INSERT ... ON CONFLICT (id) DO NOTHING`. See `docs/data/database-seeding.md`.

**Predefined UUIDs** (stable across environments — used as FK targets and constants):
- Super Admin Tenant: `00000000-0000-0000-0000-100000000000` (seeded in `Reference/0002_super_admin_tenant.sql`)
- Administrator role: `00000000-0000-0000-0000-000000000001`
- ProcurementOperator role: `00000000-0000-0000-0000-000000000002`
- ProductionWorker role: `00000000-0000-0000-0000-000000000003`
- WarehouseEmployee role: `00000000-0000-0000-0000-000000000004`
- FinanceEmployee role: `00000000-0000-0000-0000-000000000005`

## Infrastructure services

Each lives in `HrastERP.Infrastructure/<Service>/`, is configured by an `appsettings.json` section bound to a settings class validated on startup, and returns `Result` for expected failures. Read the guide before working on one:

- **Background jobs** (Hangfire) — `docs/services/background-jobs.md`. A new recurring job implements `IRecurringJobDefinition` and must be registered in DI both as `IRecurringJobDefinition` and by its concrete type (Hangfire's activator needs the latter).
- **Email** (MailKit) — `docs/services/email.md`. HTML templates are `.html` files in `Email/Templates/` with `{{placeholder}}` syntax, picked up automatically by the `<EmbeddedResource>` glob in the `.csproj`.
- **File storage** (local, tenant-isolated) — `docs/services/file-storage.md`
- **PDF generation** (QuestPDF, `IReportGenerator<TData>` per module) — `docs/services/pdf-generation.md`
- **Caching** (Redis, keys `Module:Entity:Identifier`) — `docs/services/caching.md`
- **Logging** (Serilog) — `docs/services/logging.md`
- **OpenAPI/Scalar UI and `/health` checks** — `docs/api/api-setup.md`

## Test stack

xUnit + FluentAssertions + NSubstitute. Each module has a dedicated test project under `tests/`. `xunit` is a global using in test projects — no need to add `using Xunit;`. See `docs/architecture/testing.md`.

**Mocking:** Use NSubstitute (`Substitute.For<IInterface>()`) for dependency doubles — fakes that implement interfaces (`ICurrentUser`, `ICurrentTenant`, `IRecurringJobManager`, etc.). Do NOT replace concrete test-infrastructure types (`TestDbContext`, `TestEntity`, test data records) — those stay as-is.

**Test method naming:** `ExpectedBehavior_When_Condition` — e.g., `Returns_success_When_credentials_are_valid`, `Throws_InvalidOperationException_When_TenantId_is_empty_on_insert`. `When` is always capitalized. State the assertion first, then the condition that triggers it.

**Integration tests (Testcontainers):** Tests that execute against a real database or Redis are tagged `[Trait("Category", "Integration")]` and implement `IClassFixture<PostgresContainerFixture>` or `IClassFixture<RedisContainerFixture>` (in `tests/HrastERP.Infrastructure.Tests/Fixtures/`). `PostgresContainerFixture.NewConnectionString()` returns a unique database name per call (`test_{Guid:N}`) for test isolation. Tests using a custom `TestDbContext` call `EnsureCreated()`; tests using the real `HrastDbContext` call `MigrateAsync()` in `IAsyncLifetime.InitializeAsync()`.
