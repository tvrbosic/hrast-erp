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

**Modular monolith with vertical slice architecture** — single deployable unit, five independent business modules (Administration, Finance, Inventory, Procurement, Production). Each module is a **single project** with Clean Architecture layers as folders (Domain, Application, Infrastructure). The API project is the composition root and owns all controllers.

**Project naming convention:** `HrastERP.<Module>` (e.g. `HrastERP.Inventory`). Each module contains `Domain/`, `Application/`, and `Infrastructure/` folders.

**Dependency wiring:** `HrastERP.API` references all module projects. Each module exposes a `Add<Module>Module()` extension method that registers MediatR handlers, FluentValidation validators, and EF Core configurations. Controllers live in the API project and are discovered automatically. Shared infrastructure is registered via a single `AddInfrastructure()` call, which delegates to focused extension classes colocated with each concern: `DatabaseServiceExtensions`, `BehaviorServiceExtensions`, `AuthenticationServiceExtensions`, `BackgroundJobServiceExtensions`, `EmailServiceExtensions`, `FileStorageServiceExtensions`, `PdfGenerationServiceExtensions`, and `CacheServiceExtensions`. Exception: `LoggingServiceExtensions` is called directly on `WebApplicationBuilder` in `Program.cs` (not through `AddInfrastructure()`) because Serilog's `UseSerilog()` requires access to `builder.Host`.

**CQRS:** MediatR with commands and queries organized by feature inside `Application/`. Structure: `Application/<Feature>/Commands/` and `Application/<Feature>/Queries/`. Handlers return `Result<T>`.

**Data access:** Handlers inject `HrastDbContext` directly — no repository pattern. With CQRS, each handler has a single focused query or command; repositories would be pass-through wrappers.

**Pipeline behaviors** (registered in `HrastERP.Infrastructure`), in execution order:
- `LoggingBehavior` — structured request/response logging with timing and sanitized request payload; properties marked with `[SensitiveData]` are masked as `"***"`; outermost wrapper
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
- **`ITenantEntity`** — domain marker interface for entities requiring row-level tenant isolation; exposes `Guid TenantId { get; }`. Entities opt in explicitly — `BaseEntity` does NOT implement it. `TenantId` is auto-populated by `TenantEntityInterceptor` on insert when `Guid.Empty`; if already set, the interceptor skips it (allows super admin to create entities for other tenants).
- **`IDomainEvent`** — marker interface for domain events; aggregates raise them, the application layer dispatches after commit
- **`ValueObject`** — equality by `GetEqualityComponents()`; use for `Money`, `Address`, etc.
- **`Result` / `Result<TValue>`** — all command and query handlers return these instead of throwing for expected failures; supports implicit conversion from `TValue` and `Error`
- **`Error`** — `record(string Code, string Message, ErrorType Type)`; code is dot-separated e.g. `"Order.NotFound"`; `ErrorType` enum: `Validation`, `NotFound`, `Forbidden`, `Conflict`, `Unexpected`; factory methods: `Error.NotFound`, `Error.Validation`, `Error.Forbidden`, `Error.Conflict`, `Error.Unexpected`. `Error.Validation` accepts an optional `validationErrors` (`IReadOnlyDictionary<string, string[]>?`) for field-level error detail; populated automatically by `ValidationBehavior`. Each module defines errors as `static readonly` constants in a `<Module>Errors` class. See `docs/error-handling.md` and `docs/api-responses.md`.
- **`PagedResult<T>`** — returned by all list queries; created via `PagedResult<T>.Create(...)`
- **`Permission`** — `[Flags] enum Permission : long` in `HrastERP.SharedKernel/Authorization/`; 20 CRUD permissions across 5 modules (bits 0–19). Use for authorization checks — always reference named enum members, never raw `long` values.
- **`[SensitiveData]`** — marker attribute (`HrastERP.SharedKernel/Logging/`) for MediatR request properties containing sensitive data (passwords, tokens). `LoggingBehavior` masks these with `"***"` when logging request payloads. Apply with `[property: SensitiveData]` on record constructor parameters.
- **`ICurrentUser`** / **`ICurrentTenant`** — injected into application handlers; implemented in API layer from JWT claims. `ICurrentUser.EffectivePermissions` returns the current user's combined permissions (parsed from the `"permissions"` JWT claim). These represent ambient request context and belong in SharedKernel. Infrastructure service interfaces (email, file storage, jobs) belong in `HrastERP.Infrastructure` instead.
- **`TenantConstants`** — `SuperAdminTenantId` (`00000000-0000-0000-0000-100000000000`), defined in `HrastERP.SharedKernel/Constants/`. Users belonging to this tenant bypass tenant query filters and can operate across all tenants. Their role permissions are still enforced.
- **Global exception middleware** (`GlobalExceptionMiddleware`, in `HrastERP.API/Middleware/`) — registered as the first middleware in `Program.cs`; catches any unhandled infrastructure/framework exception and returns 500 with `{ code: "General.Unexpected", message: "An unexpected error occurred." }`. Application-layer failures always use `Result.Failure` — the middleware is a safety net only, not the primary error path.
- **Model binding error factory** (`ModelBindingExtensions.ConfigureModelBindingErrorFormat()`, in `HrastERP.API/Extensions/`) — replaces ASP.NET Core's default `InvalidModelStateResponseFactory` so that model binding failures (malformed JSON, missing `[Required]` fields, type mismatches) return the same `ErrorResponse` shape as application validation: HTTP 422 with `{ code: "General.Validation", message: "...", errors: { ... } }` and camelCase field names.
- **API response envelope** (`ApiResponse<T>`, in `HrastERP.API/Responses/`) — all successful responses from `ToActionResult()` (in `HrastERP.API/Extensions/ResultExtensions`) are wrapped as `{ "data": T }`. For `PagedResult<T>`, the envelope includes a `"meta"` field with pagination info (`page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`). Error responses use `ErrorResponse` (same `Responses/` folder) and are **not** enveloped — they remain flat `{ "code", "message", "errors?" }`. Both types are the only response shapes the API produces; all endpoints must use them consistently.

**Audit fields:** All entities get `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` auto-populated by `AuditableEntityInterceptor` in the Infrastructure layer. Uses `DateTime` (UTC) and `ICurrentUser.UserId` (`Guid`). Falls back to `Guid.Empty` when unauthenticated.

**Soft delete:** All entities get `DeletedAt`/`DeletedBy` auto-populated by `SoftDeleteInterceptor` when deleted. A global query filter hides soft-deleted entities by default.

**Tenant isolation:** Entities opt in to row-level tenant scoping by implementing `ITenantEntity`. `ApplicationUser` implements `ITenantEntity`. `TenantEntityInterceptor` auto-populates `TenantId` from `ICurrentTenant` on insert only when `TenantId` is `Guid.Empty` — if already set, it skips (allows super admin to create entities for other tenants). Throws `InvalidOperationException` if `TenantId` is still `Guid.Empty` after population — a programming error caught before reaching the database. A global query filter restricts all `ITenantEntity` queries to the current tenant; when `CurrentTenantId == TenantConstants.SuperAdminTenantId`, the tenant query filter is bypassed (all `ITenantEntity` records visible). `TenantValidationBehavior` provides an early defense at the pipeline level, returning `Result.Failure(Error.Forbidden("General.MissingTenant", ...))` before any handler executes when tenant context is missing.

**Audit log:** All entity state changes (create, update, soft-delete) on `IAuditable` entities are automatically captured by `AuditLogInterceptor` into the `audit_log` table. Each `AuditLogEntry` records `EntityName`, `EntityId`, `Action` (Created/Updated/Deleted/Purged), `OldValues`/`NewValues` (JSON), `UserId`, `TenantId`, and `Timestamp`. The entry is a plain class implementing `ITenantEntity` — it does NOT inherit `BaseEntity<TId>`. The interceptor runs last in the chain (after `AuditableEntityInterceptor`, `SoftDeleteInterceptor`, `TenantEntityInterceptor`) so it sees final entity state. Soft-deletes are detected by checking if `DeletedAt` was modified. Value capture: Created → full snapshot in `NewValues`; Updated → changed properties only; Deleted → full snapshot in `OldValues`. The table is append-only. Located at `HrastERP.Infrastructure/Database/Audit/`.

Nothing in SharedKernel should import from any module.

## Module structure

Each module follows this folder layout:

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
└── <Module>Module.cs          # DI registration entry point
```

**Controllers live in `HrastERP.API/Controllers/`**, not in modules. This avoids a circular dependency (API → Module → API for shared Web types like `ResultExtensions`, `RequirePermissionAttribute`, `ErrorResponse`). The API project references all modules and all API types, so controllers there can reference both without issues.

See `docs/modules.md` for the full guide on module architecture and adding new modules.

## Administration Module

First business module — manages tenants and users.

**Tenant Management:** `Tenant` entity in `Administration/Domain/Entities/` with `Name` and `IsActive`. Full CRUD at `api/admin/tenants`, protected by `[RequireSuperAdminTenant]` + `[RequirePermission]`. Tenant inherits `BaseEntity<Guid>` but does NOT implement `ITenantEntity` — tenants don't belong to other tenants.

**User Management:** Operates directly on `ApplicationUser` (no separate User entity). Uses `UserManager` for identity operations (create, password), `HrastDbContext` for reads and field updates. Endpoints at `api/admin/users`, protected by `[RequirePermission]`. Super admin tenant users manage all tenants' users; regular tenant admins manage only their own tenant's users (via automatic tenant query filter on `ApplicationUser`).

**Super Admin Tenant Pattern:** Users belonging to `TenantConstants.SuperAdminTenantId` bypass the tenant query filter on all `ITenantEntity` queries. Their role permissions are still enforced. The admin tenant is seeded in `Reference/0002_super_admin_tenant.sql`.

**Block Inactive Entity Middleware:** `BlockInactiveEntityMiddleware` runs after authentication, before authorization. Blocks requests from users with inactive accounts or belonging to inactive tenants. Uses `ICacheService` for cached status lookups (5-min TTL). Super admin tenant users are never blocked.

**No Repository Pattern:** All handlers inject `HrastDbContext` directly. Rationale: with CQRS, each handler has a single focused query — repositories would be thin pass-through wrappers with no added value.

## Authentication

JWT Bearer authentication with ASP.NET Core Identity. Key components:

**Infrastructure layer** (`HrastERP.Infrastructure/Authentication/`):
- **`ApplicationUser`** — extends `IdentityUser<Guid>`, implements `ITenantEntity`; has `TenantId` (FK to `Tenant`), `FirstName`/`LastName`, `IsActive`, and a nullable `RoleId` FK + `Role` navigation (single role per user)
- **`RefreshToken`** — entity for refresh token rotation, linked to `ApplicationUser`
- **`IAuthService` / `AuthService`** — login, register, refresh, and logout flows using Identity + token service
- **`ITokenService` / `TokenService`** — generates JWT access tokens and refresh tokens
- **`AuthErrors`** — predefined `Error` constants for auth failures (e.g. `Auth.InvalidCredentials`)

**API layer** (`HrastERP.API/`):
- **`AuthController`** — endpoints: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`
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
- **`[RequireSuperAdminTenant]`** — `IAuthorizationFilter` attribute; restricts access to users belonging to `TenantConstants.SuperAdminTenantId`. Returns 403 with `ErrorResponse` if tenant doesn't match.
- **`PermissionPolicyProvider`** — `IAuthorizationPolicyProvider` that intercepts `"Permission:*"` policy names and builds them dynamically; delegates all other policies to the default provider. Registered as singleton.
- **`PermissionAuthorizationHandler`** — evaluates `(ICurrentUser.EffectivePermissions & requirement.Permission) != 0`. Registered as scoped (depends on scoped `ICurrentUser`).

**`Role` entity** (`HrastERP.Infrastructure/Authorization/`):
- Does NOT inherit `BaseEntity<TId>` — roles have no auditing, soft-delete, or tenant isolation
- `Permissions` stored as `bigint` via `HasConversion<long>()`
- Single role per user via nullable `ApplicationUser.RoleId` FK; `OnDelete(SetNull)` clears the FK on all users when a role is deleted
- See `docs/authorization.md` for the full reference

## Database Seeding

`DatabaseSeeder` (`src/HrastERP.Infrastructure/Database/DatabaseSeeder.cs`) runs at application startup via `Program.cs` before the middleware pipeline. It requires migrations to be applied first.

Plain SQL scripts live in two folders inside `src/HrastERP.Infrastructure/Database/Seeds/`:

- `Reference/` — always applied in every environment
- `Fixtures/` — applied only in `Development` environment

**Script naming:** `NNNN_description.sql` (4-digit zero-padded prefix). Scripts execute in ascending numeric order within each folder; Reference scripts always run before Fixtures.

**Idempotency:** Applied scripts are recorded in `seed_history` (a plain table created by the seeder, not managed by EF Core migrations). Scripts themselves use `INSERT ... ON CONFLICT ("Id") DO NOTHING`. A script runs exactly once per database.

**Predefined UUIDs** (stable across environments — used as FK targets and constants):
- Super Admin Tenant: `00000000-0000-0000-0000-100000000000` (seeded in `Reference/0002_super_admin_tenant.sql`)
- Administrator role: `00000000-0000-0000-0000-000000000001`
- ProcurementOperator role: `00000000-0000-0000-0000-000000000002`
- ProductionWorker role: `00000000-0000-0000-0000-000000000003`
- WarehouseEmployee role: `00000000-0000-0000-0000-000000000004`
- FinanceEmployee role: `00000000-0000-0000-0000-000000000005`

See `docs/database-seeding.md` for the full guide on adding new seed scripts.

## Background Jobs

Hangfire with PostgreSQL storage provides background job infrastructure. Configured in `appsettings.json` under `"Hangfire"` section, bound to `HangfireSettings` with validation on startup.

**Key types** (`HrastERP.Infrastructure/Hangfire/`):
- **`HangfireSettings`** — configuration: `WorkerCount` (default 1), `SoftDeleteRetentionDays` (default 90), `RevokedTokenRetentionDays` (default 7), cron expressions for each cleanup job
- **`IRecurringJobDefinition`** — (`Services/`) pure C# interface for self-registering recurring background jobs; exposes `JobId`, `CronExpression`, and `ExecuteAsync(CancellationToken)`. Implementations are auto-discovered at startup by `RecurringJobRegistrar`.
- **`IBackgroundJobService` / `HangfireBackgroundJobService`** — (`Services/`) thin wrapper over Hangfire's static APIs for `Enqueue`, `Schedule`, and `AddOrUpdateRecurring`
- **`RecurringJobRegistrar`** — (`Services/`) static class called at startup; resolves all `IRecurringJobDefinition` implementations, validates `JobId` uniqueness, and registers them with Hangfire
- **`LocalhostDashboardAuthorizationFilter`** — (`Filters/`) restricts Hangfire dashboard (`/hangfire`) to localhost connections only

**Built-in cleanup jobs** (`HrastERP.Infrastructure/Hangfire/Jobs/`):
- **`RefreshTokenCleanupJob`** — deletes expired tokens and revoked tokens past retention period; batched raw SQL (no audit — `RefreshToken` doesn't inherit `BaseEntity<TId>`)
- **`SoftDeleteCleanupJob`** — hard-deletes soft-deleted entities past retention period; uses raw SQL to bypass the interceptor chain (no HTTP context); writes `Purged` audit entries with full entity snapshot before deletion

**Adding a new recurring job:** Create a class implementing `IRecurringJobDefinition` in the appropriate module or Infrastructure layer, register it as `IRecurringJobDefinition` in DI (and by concrete type for Hangfire's activator). It will be auto-discovered at startup. See `docs/background-jobs.md` for the full guide.

## Email

MailKit-based SMTP email infrastructure. Configured in `appsettings.json` under `"Smtp"` section, bound to `SmtpSettings` with validation on startup. Development uses localhost:1025 (local SMTP server like MailHog).

**Key types** (`HrastERP.Infrastructure/Email/`):
- **`SmtpSettings`** — configuration: `Host`, `Port` (default 587), `Username`, `Password`, `FromAddress`, `FromName`, `UseSsl` (default true)
- **`IEmailService` / `MailKitEmailService`** — `SendAsync(EmailMessage)` for plain sends, `SendTemplatedAsync(templateName, placeholders, EmailMessage)` for templated sends; both return `Result`
- **`EmailMessage`** — model with `To`, `Cc`, `Bcc`, `Subject`, `Body`, `IsHtml` (default true)
- **`EmailErrors`** — predefined `Error` constants: `Email.SendFailed`, `Email.TemplateNotFound`

**HTML templates:** Embedded resources in `Email/Templates/`. Use `{{placeholder}}` syntax for variable substitution. Templates are loaded by name (e.g. `"welcome"` loads `Email/Templates/welcome.html`). Add new templates as `.html` files in that folder — they're automatically included via the `<EmbeddedResource>` glob in the `.csproj`.

See `docs/email.md` for the developer guide on sending emails and adding templates.

## File Storage

Local file storage infrastructure with tenant-isolated directory structure. Configured in `appsettings.json` under `"FileStorage"` section, bound to `FileStorageSettings` with validation on startup.

**Key types** (`HrastERP.Infrastructure/FileStorage/`):
- **`FileStorageSettings`** — configuration: `RootPath` (default `./storage/files`), `MaxFileSizeBytes` (default 10 MB), `AllowedContentTypes` (PDF, images, CSV, XLSX, DOCX)
- **`IFileStorageService` / `LocalFileStorageService`** — `UploadAsync(stream, fileName, contentType)`, `DownloadAsync(fileId)`, `DeleteAsync(fileId)`, `GetUrlAsync(fileId)`; all return `Result<T>` or `Result`
- **`StoredFile`** — entity extending `BaseEntity<Guid>` + `ITenantEntity`; tracks `FileName`, `ContentType`, `SizeInBytes`, `StoragePath` in the `stored_files` table
- **`FileUploadResult` / `FileDownloadResult`** — models returned by upload and download operations
- **`FileStorageErrors`** — predefined `Error` constants: `FileStorage.FileNotFound`, `FileStorage.FileTooLarge`, `FileStorage.ContentTypeNotAllowed`, `FileStorage.StorageFailed`

**Storage layout:** Files are stored under `{RootPath}/{TenantId}/{Year}/{Month}/{FileId}{Extension}`. The `storage/` directory is git-ignored.

See `docs/file-storage.md` for the developer guide on uploading, downloading, and managing files.

## PDF Generation

QuestPDF-based PDF report generation infrastructure. Configured in `appsettings.json` under `"PdfGeneration"` section, bound to `PdfGenerationSettings` with validation on startup.

**Key types** (`HrastERP.Infrastructure/PdfGeneration/`):
- **`PdfGenerationSettings`** — configuration: `CompanyName` (default `"Hrast ERP"`), `LogoPath` (nullable, falls back to embedded default), `PrimaryColor`, `AccentColor`, `TextColor` (hex), `FontFamily` (default `"Open Sans"`)
- **`IReportGenerator<TData>`** — generic interface for PDF report generators; each module implements this in its `Infrastructure/Reports/` folder. Synchronous `Generate(TData data)` returns `Result<ReportResult>`
- **`ReportBuilder`** — singleton service providing shared report layout: `ApplyDefaultPageSettings` (A4, 2cm margins, font), `ApplyHeader` (logo, company name, report title, date), `ApplyFooter` (timestamp, page numbering), and formatting helpers (`FormatCurrency`, `FormatDate`, color accessors)
- **`ReportResult`** — output model with `Content` (byte[]), `FileName`, `ContentType`
- **`PdfGenerationErrors`** — predefined `Error` constants: `PdfGeneration.GenerationFailed`, `PdfGeneration.LogoNotFound`

**Fonts and logo:** Open Sans fonts and a placeholder logo are embedded resources in `PdfGeneration/Assets/`. Custom logo can be set via `LogoPath` in settings.

See `docs/pdf-generation.md` for the developer guide on writing report generators.

## Caching

Redis-based distributed caching infrastructure. Configured in `appsettings.json` under `"Cache"` section, bound to `CacheSettings` with validation on startup. Development uses localhost:6379 (Docker Compose Redis container).

**Key types** (`HrastERP.Infrastructure/Caching/`):
- **`CacheSettings`** — configuration: `ConnectionString` (required), `DefaultTtlMinutes` (default 60, range 1-1440)
- **`ICacheService` / `RedisCacheService`** — `GetAsync<T>(key)`, `SetAsync<T>(key, value, expiration?)`, `RemoveAsync(key)`, `RemoveByPrefixAsync(prefix)`; non-throwing — failures are logged and return null/silently complete
- **`CacheErrors`** — predefined `Error` constants: `Cache.SerializationFailed`, `Cache.OperationFailed`

**Cache key conventions:** `Module:Entity:Identifier` (e.g. `Roles:all`, `Roles:{id}`). Prefix-based invalidation: `RemoveByPrefixAsync("Roles")` clears all role-related entries using Redis SCAN.

**Service lifetime:** Singleton. All dependencies (`IDistributedCache`, `IConnectionMultiplexer`, `CacheSettings`) are singletons.

See `docs/caching.md` for the developer guide on caching patterns and key conventions.

## Logging

Serilog-based structured logging infrastructure. Replaces the default ASP.NET Core logging provider — all existing `ILogger<T>` usage routes through Serilog automatically. Configured in `appsettings.json` under `"Serilog"` section. Log files are written to `logs/` (git-ignored).

**Sinks:** Console (human-readable, colored), Debug (debugger output), Rolling File (JSON, one file per day, 30-day retention under `./logs/`).

**Enrichment:** Every log entry is automatically enriched with `UserId`, `TenantId` (via `LoggingEnrichmentMiddleware` after authentication), `RequestId` (from `HttpContext.TraceIdentifier`), `MachineName`, and `EnvironmentName`. For unauthenticated requests, `UserId`/`TenantId` are `Guid.Empty`.

**Key types** (`HrastERP.Infrastructure/Logging/`):
- **`LoggingServiceExtensions`** — `AddLoggingInfrastructure(WebApplicationBuilder)` configures Serilog provider with enrichers and sinks; `UseLoggingInfrastructure(WebApplication)` adds HTTP request logging; `UseLoggingEnrichment(WebApplication)` adds enrichment middleware
- **`LoggingEnrichmentMiddleware`** — pushes `UserId` and `TenantId` into Serilog's `LogContext` per request; runs after `UseAuthentication()`, before `UseAuthorization()`

**Configuration (hybrid):** Structural setup (enrichers, sinks) in C# via `LoggingServiceExtensions`. Tunable values (log levels, file path, retention) in `appsettings.json` under `"Serilog"`. Per-namespace log level overrides suppress framework noise (`Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `Hangfire`, `System.Net.Http`) to `Warning` in production, relaxed to `Information` in Development.

**Middleware order in Program.cs:** `GlobalExceptionMiddleware` → `UseSerilogRequestLogging()` → ... → `UseAuthentication()` → `LoggingEnrichmentMiddleware` → `BlockInactiveEntityMiddleware` → `UseAuthorization()` → `MapControllers()` → `MapHealthChecks("/health")` → `MapOpenApi()` + `MapScalarApiReference()` (Development only).

See `docs/logging.md` for the developer guide on logging conventions and configuration.

## API Documentation & Health Checks

**OpenAPI + Scalar UI** — Development-only interactive API documentation. Configured in `HrastERP.API/Extensions/OpenApiServiceExtensions.cs`.

- `AddOpenApiServices()` registers OpenAPI document generation with a JWT `BearerAuth` security scheme applied globally
- `UseOpenApiInfrastructure()` maps the OpenAPI JSON endpoint and Scalar UI; guarded by `IsDevelopment()`
- **URLs:** OpenAPI document at `/openapi/v1.json`, Scalar UI at `/scalar/v1`

**Health checks** — `GET /health` returns JSON with per-dependency status for PostgreSQL, Redis, and SMTP. Unauthenticated (`.AllowAnonymous()`), available in all environments. Configured in `HrastERP.API/Extensions/HealthCheckServiceExtensions.cs`.

- **Response:** `{ status, checks: { database, redis, smtp }, totalDuration }` — 200 for Healthy/Degraded, 503 for Unhealthy
- **Custom SMTP check** (`HrastERP.API/HealthChecks/SmtpHealthCheck.cs`) — TCP connectivity only, 3-second timeout
- **JSON response writer** (`HrastERP.API/HealthChecks/HealthCheckResponseWriter.cs`) — formats the health report as camelCase JSON

**Adding a new health check:** Register it in `HealthCheckServiceExtensions.AddHealthCheckServices()` via `.AddCheck<T>(name)`. It will appear automatically in the `/health` response.

## Test stack

xUnit + FluentAssertions. Each module has a dedicated test project under `tests/`. `xunit` is a global using in test projects — no need to add `using Xunit;`.
