---
name: hrast-erp-error-handling
description: Covers the Result pattern, Error factory methods, ErrorType selection (Validation/NotFound/Forbidden/Conflict/Unexpected), module error catalog pattern, and implicit conversion in handlers. Use when defining errors or returning failures from HrastERP handlers.
---

# HrastERP: Error Handling

## Overview

HrastERP uses the **Result pattern** — no exceptions for expected failures. Every command and query handler returns `Result` or `Result<T>`. Errors carry a `Code`, `Message`, and `ErrorType`.

---

## ErrorType Selection Guide

| Situation | ErrorType | HTTP |
|---|---|---|
| Invalid input, business rule violation, precondition not met | `Validation` | 422 |
| Entity referenced by ID/key does not exist | `NotFound` | 404 |
| User lacks permission for the operation | `Forbidden` | 403 |
| Duplicate resource, uniqueness constraint violation | `Conflict` | 409 |
| Internal failure the user can't fix | `Unexpected` | 500 |

Quick decision:
- "The request was bad" → `Validation`
- "The thing doesn't exist" → `NotFound`
- "You're not allowed" → `Forbidden`
- "It already exists" → `Conflict`
- "Something broke internally" → `Unexpected`

---

## Defining Errors

### Factory methods

```csharp
Error.Validation("Order.InvalidQuantity", "Quantity must be greater than zero.");
Error.NotFound("Order.NotFound", "Order was not found.");
Error.Forbidden("Order.Forbidden", "You cannot modify another tenant's order.");
Error.Conflict("Order.Duplicate", "An order with this reference already exists.");
Error.Unexpected("Order.ProcessingFailed", "Failed to process the order.");
```

### Module error catalog

Define module errors as `static readonly` constants — prevents typos, enables reference equality, documents failure modes in one place:

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

**Naming:** `<Module>Errors` class, `static readonly Error` constants, error codes `<Module>.<ErrorName>`.

---

## Returning Errors from Handlers

Use implicit conversion — no explicit wrapping needed:

```csharp
public async Task<Result<Order>> Handle(GetOrderQuery query, CancellationToken ct)
{
    var order = await repository.GetByIdAsync(query.Id, ct);

    if (order is null)
        return OrderErrors.NotFound;  // implicit: Result<Order>.Failure(error)

    return order;  // implicit: Result<Order>.Success(order)
}
```

For commands with no return value:

```csharp
public async Task<Result> Handle(DeleteOrderCommand command, CancellationToken ct)
{
    // ...
    if (notFound)
        return Result.Failure(OrderErrors.NotFound);

    return Result.Success();
}
```

---

## Domain State vs. Application Errors

`Error` represents why a user's **request** failed — not domain state. Domain concepts (machine malfunction, invoice overdue) are modeled as entity statuses, enumerations, or domain events. They become `Error` only when they cause a request to fail:

```csharp
// Domain state
public enum MachineStatus { Operational, Malfunctioning, UnderMaintenance }

// Application error when an operation fails due to that state
public static readonly Error MachineUnavailable =
    Error.Validation("Production.MachineUnavailable",
        "Cannot schedule production: machine is not operational.");
```

---

## Common Mistakes

| Mistake | Correct |
|---|---|
| Throwing exceptions for expected failures | Return `Result.Failure(error)` |
| Inline error strings instead of catalog | Define in `<Module>Errors` class |
| Using `Validation` for missing entities | Use `NotFound` |
| Using `Unexpected` for business rule violations | Use `Validation` |
| Manual `Result<T>.Failure(error)` wrapping | Just `return error;` — implicit operator handles it |
