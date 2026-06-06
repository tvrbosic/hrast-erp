# MediatR Pipeline Behaviors

This document describes the MediatR pipeline behaviors registered in `HrastERP.Infrastructure` — what they do, how they are ordered, and how they interact with handlers.

---

## Overview

MediatR pipeline behaviors wrap every request/response cycle. They are the equivalent of middleware, but scoped to the CQRS pipeline. Every `IRequest<TResponse>` dispatched via `ISender.Send()` passes through all registered behaviors before reaching the handler.

Behaviors are registered in `HrastERP.Infrastructure/Extensions/BehaviorServiceExtensions.cs` and wired up automatically when `AddInfrastructure()` is called from `Program.cs`.

---

## Execution Order

Behaviors execute in registration order, outermost first:

```
LoggingBehavior → ValidationBehavior → Handler
```

`LoggingBehavior` is registered first, so it wraps the entire pipeline including validation. It starts the timer before validation runs and stops it after the handler returns (or after validation short-circuits).

---

## LoggingBehavior

**File:** `src/HrastERP.Infrastructure/Behaviors/LoggingBehavior.cs`

Logs the start and completion of every MediatR request, including elapsed time in milliseconds. Uses `ILogger<LoggingBehavior<TRequest, TResponse>>` — log entries are scoped to the concrete request type.

**Log entries emitted:**

```
[Information] Handling CreateOrderCommand
[Information] Handled CreateOrderCommand in 42ms
```

The second entry is always emitted, even when `ValidationBehavior` short-circuits before reaching the handler — the elapsed time in that case reflects validation overhead only.

**Applies to:** All requests (`where TRequest : IRequest<TResponse>`, no additional constraint).

---

## ValidationBehavior

**File:** `src/HrastERP.Infrastructure/Behaviors/ValidationBehavior.cs`

Runs all `IValidator<TRequest>` implementations registered for the request type before the handler executes. If any validation failures are found, the pipeline is short-circuited and a `Result.Failure` is returned — the handler never runs.

**Applies to:** Only requests where `TResponse : Result` (i.e. all command and query handlers in this codebase).

### How it works

1. Collects all `IValidator<TRequest>` registered in DI for the current request type.
2. If none are registered, passes through immediately.
3. Runs all validators in parallel via `Task.WhenAll`.
4. Flattens all `ValidationFailure` instances from all validators.
5. Groups failures by property name, converting to camelCase (e.g. `FirstName` → `firstName`).
6. Returns `Result.Failure` with an `Error.Validation` carrying the field-level dictionary.

### Returned error

```csharp
Error.Validation(
    "General.Validation",
    "One or more validation errors occurred.",
    fieldErrors  // Dictionary<string, string[]>
)
```

The `fieldErrors` dictionary maps camelCase property names to arrays of error messages:

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

See `docs/error-handling.md` for the full HTTP response format and how `ToActionResult()` maps this to HTTP 422.

### Registering a validator

Validators are registered per-module in each module's `Add<Module>Module()` extension method using FluentValidation's assembly scanning:

```csharp
services.AddValidatorsFromAssembly(assembly);
```

Write validators as standard FluentValidation `AbstractValidator<T>` subclasses:

```csharp
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
```

No additional wiring is needed — `ValidationBehavior` discovers validators via DI automatically.

### Multiple validators

If multiple validators exist for the same request type, all of them run in parallel and their failures are merged. This is uncommon but supported — useful when validators are split by concern (e.g. one for input shape, one for business rules that require async DB lookups).

---

## Registration

**File:** `src/HrastERP.Infrastructure/Extensions/BehaviorServiceExtensions.cs`

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

Both are registered as open generics and resolved per-request by MediatR. Registration order determines pipeline order — `LoggingBehavior` first means it is the outermost wrapper.
