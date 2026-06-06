# Error Handling Strategy

This document describes how errors are defined, propagated, and returned to API consumers in Hrast ERP.

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
// With field-level detail (ValidationBehavior does this automatically for FluentValidation failures):
Error.Validation("General.Validation", "One or more validation errors occurred.",
    new Dictionary<string, string[]> { ["quantity"] = ["Must be greater than zero."] });
Error.NotFound("Order.NotFound", "Order was not found.");
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

**Naming convention:** `<Module>Errors` class with `static readonly Error` constants. Error codes follow the pattern `<Module>.<ErrorName>`.

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

## Mapping Results to HTTP Responses

### `ResultExtensions.ToActionResult()`

The API project provides an extension method that maps `ErrorType` to HTTP status codes. Controllers call it explicitly:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Get(Guid id, CancellationToken ct)
{
    var result = await sender.Send(new GetOrderQuery(id), ct);
    return result.ToActionResult();
}
```

This returns:
- `200 OK` with the serialized value on success
- The appropriate error status code (422/403/404/409/500) with `{ code, message }` body on failure; validation (422) responses also include `errors` with field-level messages

### Custom success responses

When the default `200 OK` is not appropriate (e.g. `201 Created` or `204 NoContent`), handle the success branch inline:

```csharp
[HttpPost]
public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
{
    var result = await sender.Send(new CreateOrderCommand(request), ct);

    return result.IsSuccess
        ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
        : result.ToActionResult();
}

[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
{
    var result = await sender.Send(new DeleteOrderCommand(id), ct);

    return result.IsSuccess
        ? NoContent()
        : result.ToActionResult();
}
```

### Global exception middleware

`GlobalExceptionMiddleware` (registered first in `Program.cs`) is a safety net for unhandled infrastructure or framework exceptions — database connectivity failures, unexpected nulls, framework bugs. It logs the exception and returns:

```json
{
    "code": "General.Unexpected",
    "message": "An unexpected error occurred."
}
```

This middleware is **not** the primary error path. Application-layer failures always use `Result.Failure` with an appropriate `ErrorType`. The middleware only fires if something truly unexpected escapes the handler pipeline.

---

### Error response body

All error responses follow a consistent shape:

```json
{
    "code": "Auth.InvalidCredentials",
    "message": "Invalid email or password."
}
```

Validation responses (422) additionally include an `errors` field with per-field messages. The `errors` key is omitted entirely from non-validation responses (404, 403, 409, 500).

```json
{
    "code": "General.Validation",
    "message": "One or more validation errors occurred.",
    "errors": {
        "quantity": ["Must be greater than zero."],
        "name": ["Name is required.", "Name must not exceed 100 characters."]
    }
}
```

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
