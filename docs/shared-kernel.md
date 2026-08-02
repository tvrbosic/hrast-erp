# SharedKernel

`HrastERP.SharedKernel` is a class library referenced by all module layers (Domain, Application, Infrastructure). It contains base types, cross-cutting abstractions, and utilities that are not specific to any single business module.

Nothing in SharedKernel should import from any module. It has no dependency on EF Core, MediatR, or any other framework — it is pure C#.

## Folder Quick Reference

| Folder | Purpose | What goes here | What does NOT go here |
|---|---|---|---|
| `Domain/` | Domain model building blocks | Base entities, aggregate roots, value objects, domain marker interfaces (`IAuditable`, `ISoftDeletable`, `ITenantEntity`, `IDomainEvent`) | Business logic, module-specific entities |
| `Results/` | Operation outcome types | `Result<T>`, `Error`, `ErrorType` — success/failure representation | Data envelopes, pagination, DTOs |
| `Abstractions/` | Ambient request context | `ICurrentUser`, `ICurrentTenant` — "who is calling?" | Infrastructure service interfaces (email, file storage, jobs) |
| `Authorization/` | Authorization primitives | `Permission` flags enum | Authorization handlers, policy providers |
| `Common/` | Shared utility types | `PagedResult<T>`, sorting descriptors, date/time wrappers | Domain model types, result types |

---

## Domain

Contains the base types for building a domain model. Every entity, aggregate root, and value object in every module inherits from these classes.

**When to add here:** Base classes or interfaces that define how domain objects are structured, compared, or behave — shared building blocks, not business logic.

### `IDomainEvent.cs`
Marker interface implemented by all domain events across modules. A domain event represents something that has already happened within the domain (e.g. `OrderApproved`, `GoodsReceived`). Aggregates raise events by adding them to their internal list; the application layer dispatches them after the transaction commits.

### `BaseEntity.cs`
Abstract generic base class `BaseEntity<TId>` for all entities. Implements both `IAuditable` and `ISoftDeletable`, so every entity automatically carries audit trail and soft-delete fields. Provides an `Id` property and overrides `Equals`, `GetHashCode`, `==`, and `!=` so that two entity instances are considered equal if and only if they have the same type and the same `Id`. Also includes a protected parameterless constructor required by EF Core.

### `AggregateRoot.cs`
Extends `BaseEntity<TId>` with domain event support. Maintains an internal list of `IDomainEvent` instances and exposes them as `IReadOnlyCollection<IDomainEvent> DomainEvents`. Subclasses raise events by calling the protected `AddDomainEvent` method. The application layer calls `ClearDomainEvents` after dispatching them. Inherits audit trail and soft-delete support from `BaseEntity`.

### `IAuditable.cs`
Interface for entities with audit trail fields: `CreatedAt` (`DateTime`), `CreatedBy` (`Guid`), `UpdatedAt` (`DateTime?`), and `UpdatedBy` (`Guid?`). Implemented by `BaseEntity`, so all entities are auditable. The `AuditableEntityInterceptor` in the Infrastructure layer detects entities implementing this interface and auto-populates the fields on `SaveChanges`. `CreatedBy`/`UpdatedBy` store `ICurrentUser.UserId`.

### `ISoftDeletable.cs`
Interface for entities that support soft deletion. Exposes two nullable properties: `DeletedAt` (`DateTime?`, UTC) and `DeletedBy` (`Guid?`, user ID). Implemented by `BaseEntity`, so all entities are soft-deletable. `DeletedAt` and `DeletedBy` are populated automatically by `SoftDeleteInterceptor` — never set them in domain code.

**Soft-delete behavior (Infrastructure):** `SoftDeleteInterceptor` intercepts every `SaveChanges` call. When an entity's EF Core state is `Deleted`, the interceptor flips it to `Modified` and sets `DeletedAt = DateTime.UtcNow` and `DeletedBy = currentUser.UserId`. A global query filter (`DeletedAt == null`) is applied to all `ISoftDeletable` entity types in `HrastDbContext`, so soft-deleted records are invisible to normal queries. Use `.IgnoreQueryFilters()` to include them (e.g. for admin views or purge jobs).

### `ITenantEntity.cs`
Domain marker interface for entities that require row-level tenant isolation. Exposes a single `Guid TenantId { get; }` property — implementing entities add a setter for EF Core materialization.

Entities opt in by implementing this interface; `BaseEntity` does **not** implement it because not all entities are tenant-scoped (e.g. a `Tenant` entity itself, or system-wide lookup tables).

**Tenant isolation behavior (Infrastructure):** `TenantEntityInterceptor` intercepts every `SaveChanges` call. When a new entity implementing `ITenantEntity` is added, the interceptor sets `TenantId` from `ICurrentTenant.TenantId` and throws `InvalidOperationException` if the result is `Guid.Empty` — a programming error, not a user-recoverable failure. Modified entities are never re-assigned; tenant reassignment must never happen.

A global query filter in `HrastDbContext` compares `entity.TenantId == currentTenant.TenantId` for all `ITenantEntity` types, so cross-tenant records are invisible to normal queries. Use `.IgnoreQueryFilters()` to bypass (admin-only scenarios).

A MediatR pipeline behavior (`TenantValidationBehavior`) provides early defense: before any handler executes, it checks `ICurrentTenant.TenantId != Guid.Empty` and short-circuits with `Result.Failure(Error.Forbidden("General.MissingTenant", ...))` when tenant context is missing.

### `ValueObject.cs`
Abstract base class for value objects. Equality is determined by the values returned from the abstract `GetEqualityComponents()` method, not by reference or identity. Overrides `Equals`, `GetHashCode`, `==`, and `!=` accordingly. Use this for types like `Money`, `Address`, or `Dimensions` that have no identity of their own.

---

## Results

Contains the Result pattern types used to represent the outcome of an operation explicitly, without throwing exceptions for expected failure cases.

**When to add here:** Types that model success or failure as return values. All command handlers and query handlers in the application layer return `Result` or `Result<T>`.

### `Error.cs`
Immutable record `Error(string Code, string Message, ErrorType Type)` representing a named, categorized failure reason. The `Code` is a dot-separated string identifying the error (e.g. `"User.NotFound"`, `"Order.AlreadyCancelled"`). The `Type` is an `ErrorType` enum that classifies the error for HTTP response mapping. Provides static factory methods `NotFound`, `Validation`, `Forbidden`, `Conflict`, and `Unexpected` as semantic constructors that set `Type` automatically, and a `None` constant representing the absence of an error. See `docs/error-handling.md` for the full error handling strategy.

### `ErrorType.cs`
Enum defining the categories of errors: `Validation` (400), `NotFound` (404), `Forbidden` (403), `Conflict` (409), and `Unexpected` (500). Used by `ResultExtensions.ToActionResult()` in the API layer to map errors to HTTP status codes.

### `Result.cs`
Two closely related types:

- `Result` — non-generic, used by commands that produce no value on success. Has `IsSuccess`, `IsFailure`, and `Error` properties. Created via `Result.Success()` or `Result.Failure(Error)`.
- `Result<TValue>` — generic, used by queries and commands that return a value. Adds a `Value` property that throws `InvalidOperationException` if accessed on a failed result. Supports implicit conversion from `TValue` (creates a success) and from `Error` (creates a failure), which allows handlers to return values or errors directly without wrapping them manually.

---

## Abstractions

Contains interfaces that give application-layer code access to the current request context — who is calling and on behalf of which tenant. These are injected via DI and implemented in the API layer.

**When to add here:** Interfaces that represent ambient request context — information about who is making the current request. These are pure C# contracts with no framework dependencies. Implementations live in `HrastERP.API` (reading from JWT claims), never here. Do NOT put infrastructure service interfaces here (email, file storage, background jobs) — those belong in `HrastERP.Infrastructure` alongside their implementations.

### `ICurrentTenant.cs`
Provides the `TenantId` of the current request. Implemented in the API layer by reading the `TenantId` claim from the JWT. Used in EF Core global query filters and in application layer validation to enforce tenant isolation.

### `ICurrentUser.cs`
Provides identity and permission information for the authenticated user making the current request: `UserId`, `TenantId`, `Username`, `IsAuthenticated`, and `EffectivePermissions` (the `Permission` flags enum representing the user's combined permissions). Consumed by application handlers and authorization checks.

---

## Authorization

Contains authorization primitives shared across all modules.

**When to add here:** Types that define the authorization model — permission definitions, policy names. Do NOT put authorization handlers or policy providers here — those belong in `HrastERP.API/Authorization/`.

### `Permission.cs`
`[Flags] enum Permission : long` defining all CRUD permissions across the five business modules (bits 0–19). Used by the `[RequirePermission]` attribute on controller endpoints and by `ICurrentUser.EffectivePermissions` for authorization checks. Always reference named enum members, never raw `long` values.

---

## Common

General-purpose types that do not belong to a specific concern but are shared across all modules.

**When to add here:** Reusable utility types with no business-module affiliation — pagination envelopes, sorting descriptors, date/time wrappers, etc. Types here should be pure data carriers or utility abstractions, not domain model building blocks (those go in `Domain/`).

### `PagedResult.cs`
Generic `PagedResult<T>` returned by all list queries. Carries the `Items` collection along with pagination metadata: `TotalCount`, `Page`, `PageSize`, and computed properties `TotalPages`, `HasPreviousPage`, and `HasNextPage`. Created via the static `Create` factory method.
