# Error Handling Strategy

This document describes how errors are defined, propagated, and returned from application handlers in Hrast ERP.

---

## Overview

Hrast ERP uses the **Result pattern** instead of exceptions for expected failures. Every command and query handler returns `Result` or `Result<T>`. The `Error` record carries three pieces of information:

- **`Code`** — a dot-separated identifier (e.g. `"Auth.InvalidCredentials"`, `"Inventory.InsufficientStock"`)
- **`Message`** — a human-readable description of what went wrong
- **`Type`** — an `ErrorType` enum that categorizes the error for HTTP response mapping

---

## ErrorType Enum

| ErrorType    | HTTP Status | When to use |
|-------------|-------------|-------------|
| `Validation` | 422 Unprocessable Entity | Invalid input, business rule violation, precondition not met |
| `Unauthorized` | 401 Unauthorized | The caller could not be authenticated (wrong credentials, invalid refresh token) |
| `NotFound`   | 404 Not Found | Requested entity does not exist |
| `Forbidden`  | 403 Forbidden | User lacks permission for the operation |
| `Conflict`   | 409 Conflict | Duplicate resource, concurrent modification, already exists |
| `Unexpected` | 500 Internal Server Error | Unexpected internal failures |

---

## Defining Errors

### Factory methods on `Error`

The `Error` record provides factory methods that set the `ErrorType` automatically:

```csharp
Error.Validation("Order.InvalidQuantity", "Quantity must be greater than zero.");
// With field-level detail (ValidationBehavior does this through GeneralErrors.Validation for FluentValidation failures):
Error.Validation("Order.InvalidLines", "One or more order lines are invalid.",
    new Dictionary<string, string[]> { ["quantity"] = ["Must be greater than zero."] });
Error.NotFound("Order.NotFound", "Order was not found.");
Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
Error.Forbidden("Order.Forbidden", "You cannot modify another tenant's order.");
Error.Conflict("Order.Duplicate", "An order with this reference already exists.");
Error.Unexpected("Order.ProcessingFailed", "Failed to process the order.");
```

### Module error catalogs

Each module defines its errors as `static readonly` constants in a dedicated `Errors` class. This prevents typos, enables reference equality checks, and documents the module's failure modes in one place.

```csharp
// In HrastERP.Inventory/Domain/ or Application/
public static class InventoryErrors
{
    public static readonly Error InsufficientStock =
        Error.Validation("Inventory.InsufficientStock", "Not enough stock to fulfill the request.");

    public static readonly Error WarehouseNotFound =
        Error.NotFound("Inventory.WarehouseNotFound", "Warehouse does not exist.");

    public static readonly Error DuplicateSku =
        Error.Conflict("Inventory.DuplicateSku", "A product with this SKU already exists.");
}
```

**Naming convention:** `<Module>Errors` class with `static readonly Error` constants. Error codes follow the pattern `<Area>.<ErrorName>`, where the area is the entity or module the error is about (e.g. `User.NotFound`, `Inventory.InsufficientStock`).

**Runtime details:** an error whose message or field errors are only known at runtime is a static factory method in the catalog, never built inline in the handler:

```csharp
public static Error RoleInUse(int userCount) =>
    Error.Conflict("Role.InUse", $"Role is assigned to {userCount} users. Reassign them before deleting.");

public static Error UserCreationFailed(IReadOnlyDictionary<string, string[]> fieldErrors) =>
    Error.Validation("User.CreationFailed", "Failed to create user.", fieldErrors);
```

**Other catalogs:** Infrastructure services and the API layer have their own catalogs (`AuthErrors`, `FileStorageErrors`, `ApiErrors`, ...). The cross-cutting `General.*` errors live in `GeneralErrors` (SharedKernel `Results/`): `Validation(fieldErrors)`, `Unauthorized`, `Forbidden`, `MissingTenant`, `InactiveEntity` and `Unexpected`. They are raised by pipeline behaviors, middleware and the API layer; a module handler returns its own catalog's errors, so the code names the actual failure.

**Rules:** never call `Error.*(...)` outside a `*Errors` catalog, and define each code once. `ErrorCatalogTests` reads every catalog by reflection and fails if two of them define the same code (reusing a `GeneralErrors` error, as `ApiErrors.RouteIdMismatch` does, is allowed).

---

## Returning Errors from Handlers

Handlers return `Result<T>` or `Result`. Use implicit conversion to return errors directly:

```csharp
public async Task<Result<Order>> Handle(GetOrderQuery query, CancellationToken ct)
{
    var order = await repository.GetByIdAsync(query.Id, ct);

    if (order is null)
        return OrderErrors.NotFound;  // Implicit operator (see Result.cs): same as Result<order>.Failure(error)

    return order;  // Implicit operator (see Result.cs): same as Result<order>.Success(order)
}
```

Both conversions are powered by `implicit operator` declarations in `Result<TValue>`
(`src/HrastERP.SharedKernel/Results/Result.cs`, lines 66 and 69).
The compiler applies them automatically whenever it sees a type mismatch between
`Error`/`TValue` and `Result<TValue>` — no explicit wrapping needed.

---

## Pre-checks and Database Constraints

Check business rules such as "is this name taken?" with a query in the handler and return the matching
`Result` error (e.g. `Role.NameTaken`). Back the rule with a database constraint (unique index, Restrict FK)
as a safety net for data integrity.

If the unique index also covers rows that the query filters hide (soft-deleted rows, or rows in other
tenants), run the pre-check with `IgnoreQueryFilters()`. Otherwise the check passes, the index rejects the
insert, and the API returns a 500. For example, users keep their email after a soft delete until
`SoftDeleteCleanupJob` purges them.

Handlers do not catch `DbUpdateException`. If a concurrent request slips in between the pre-check and
`SaveChangesAsync`, the constraint rejects the write and `GlobalExceptionMiddleware` returns a 500. This is
an accepted trade-off: the race is rare, and the data stays consistent.

### Deleting entities that others depend on

A delete handler never removes, reassigns, or cascades to dependent records. Before deleting, it counts
every kind of record that still refers to the entity and, if any exist, returns a `Conflict` error that
names the dependents and their count, so the caller knows what to clear first:

```csharp
public static Error RoleInUse(int userCount) =>
    Error.Conflict("Role.InUse", $"Role is assigned to {userCount} users. Reassign them before deleting.");
```

Use one error per kind of dependent (`Tenant.HasUsers`, `Tenant.HasFiles`) and check them in order, returning
the first that applies. Count with `IgnoreQueryFilters()` so dependents in other tenants or inactive ones are
not missed; add `DeletedAt == null` for soft-deletable dependents, since the purge job removes those anyway.

Check dependents even when no foreign key links them (e.g. `StoredFile.TenantId`): without a constraint,
nothing else stops them from being orphaned. When a new entity refers to an existing one, add its check to
that entity's delete handler.

---

## Error Type Selection Guide

**"The request was bad"** → `Validation`
The input itself is wrong, or a business rule prevents the operation given current state. Examples: missing required field, negative quantity, scheduling production on a malfunctioning machine.

**"The thing doesn't exist"** → `NotFound`
The entity referenced by an ID, code, or key does not exist in the system. Examples: order not found, user not found, warehouse not found.

**"You're not allowed"** → `Forbidden`
The operation exists and the input is valid, but the current user doesn't have permission. Examples: modifying another tenant's data, accessing an admin-only feature.

**"It already exists or conflicts"** → `Conflict`
The operation would create a duplicate or violates a uniqueness constraint. Examples: duplicate SKU, duplicate email registration, concurrent edit conflict.

**"Something broke internally"** → `Unexpected`
An internal failure that the user can't fix by changing their request. Examples: external service timeout, file system error, unexpected null. These should be rare — most errors should be one of the above.

---

## Domain Errors vs. Application Errors

The `Error` type represents **application-layer operation failures** — reasons why a user's request could not be fulfilled. It is not for modeling domain state.

Domain concepts like "machine malfunction" or "invoice overdue" are modeled as entity statuses, domain events, value objects, or enumerations within the domain layer. They become `Error` values only when they cause an operation to fail:

```csharp
// Domain: machine status is domain state
public enum MachineStatus { Operational, Malfunctioning, UnderMaintenance }

// Application: operation failure when trying to use a broken machine
public static readonly Error MachineUnavailable =
    Error.Validation("Production.MachineUnavailable",
        "Cannot schedule production: machine is not operational.");
```
