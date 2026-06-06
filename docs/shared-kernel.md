# SharedKernel

`HrastERP.SharedKernel` is a class library referenced by all module layers (Domain, Application, Infrastructure). It contains base types, cross-cutting abstractions, and utilities that are not specific to any single business module.

Nothing in SharedKernel should import from any module. It has no dependency on EF Core, MediatR, or any other framework — it is pure C#.

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

**When to add here:** Interfaces for cross-cutting runtime context (identity, tenancy, current time). Implementations live in `HrastERP.API` or module Infrastructure projects, never here.

### `ICurrentTenant.cs`
Provides the `TenantId` of the current request. Implemented in the API layer by reading the `TenantId` claim from the JWT. Used in EF Core global query filters and in application layer validation to enforce tenant isolation.

### `ICurrentUser.cs`
Provides identity and permission information for the authenticated user making the current request: `UserId`, `TenantId`, `Username`, `IsAuthenticated`, and a `Permissions` collection. Consumed by application handlers and authorization checks.

---

## Common

General-purpose types that do not belong to a specific concern but are shared across all modules.

**When to add here:** Reusable utility types with no business-module affiliation — pagination, sorting descriptors, date/time wrappers, etc.

### `PagedResult.cs`
Generic `PagedResult<T>` returned by all list queries. Carries the `Items` collection along with pagination metadata: `TotalCount`, `Page`, `PageSize`, and computed properties `TotalPages`, `HasPreviousPage`, and `HasNextPage`. Created via the static `Create` factory method.

---

## Exceptions

Custom exception types for well-known error conditions. These are thrown by application handlers when something structurally wrong occurs and caught by the global exception handler middleware in the API layer, which maps them to appropriate HTTP status codes.

**When to add here:** Exception types that map to a specific HTTP response or that carry structured error data. Do not put domain-specific exceptions here — only generic cross-cutting ones.

### `NotFoundException.cs`
Thrown when a requested entity does not exist. Takes an entity name and id; formats them into the message automatically (e.g. `"User with id '42' was not found."`). The global exception handler maps this to HTTP 404.

### `ForbiddenException.cs`
Thrown when the current user lacks permission to perform an operation. Accepts an optional custom message; defaults to `"Access is forbidden."`. The global exception handler maps this to HTTP 403.

### `ValidationException.cs`
Thrown by the MediatR validation pipeline behavior when FluentValidation reports failures. Carries an `Errors` dictionary keyed by field name, with an array of error messages per field. The global exception handler maps this to HTTP 422 and includes the errors in the response body.
