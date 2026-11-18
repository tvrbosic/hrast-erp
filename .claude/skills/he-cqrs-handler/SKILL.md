---
name: he-cqrs-handler
description: Guides writing MediatR command and query handlers in HrastERP — handler structure, FluentValidation validator placement, controller wiring with ISender, ToActionResult() mapping, and success status codes (ToCreatedResult 201, Result 204). Use when implementing a new command or query.
---

# HrastERP: Writing a CQRS Handler

## Overview

Every user-facing operation is a MediatR command or query. Handlers live in the module's `Application/<Feature>/Commands/` or `Application/<Feature>/Queries/` folder. The pipeline runs `LoggingBehavior → TenantValidationBehavior → ValidationBehavior → Handler` automatically.

---

## Command Structure

```
Application/
  Orders/
    Commands/
      CreateOrder/
        CreateOrderCommand.cs
        CreateOrderCommandHandler.cs
        CreateOrderCommandValidator.cs    # optional
    Queries/
      GetOrderById/
        GetOrderByIdQuery.cs
        GetOrderByIdQueryHandler.cs
    Models/
      OrderResponse.cs                    # what queries return; also the API contract
```

Commands and queries are `public sealed record`s (the controller binds or creates them); handlers and validators are `internal sealed`. Handlers inject `HrastDbContext` and use `Set<T>()` — the context declares no `DbSet` properties. Type naming rules: `docs/architecture/type-naming.md`.

```csharp
// CreateOrderCommand.cs
public sealed record CreateOrderCommand(Guid CustomerId, int Quantity) : IRequest<Result<Guid>>;

// CreateOrderCommandHandler.cs
internal sealed class CreateOrderCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var customerExists = await dbContext.Set<Customer>().AnyAsync(c => c.Id == command.CustomerId, ct);
        if (!customerExists)
            return OrderErrors.CustomerNotFound;  // implicit conversion to Result<Guid>.Failure

        var order = Order.Create(Guid.NewGuid(), command.CustomerId, command.Quantity);
        dbContext.Set<Order>().Add(order);
        await dbContext.SaveChangesAsync(ct);

        return order.Id;  // implicit conversion to Result<Guid>.Success
    }
}
```

---

## Query Structure

```csharp
// Models/OrderResponse.cs — always in Application/<Feature>/Models/, even if only one query uses it
public sealed record OrderResponse(Guid Id, int Quantity);

// GetOrderByIdQuery.cs
public sealed record GetOrderByIdQuery(Guid Id) : IRequest<Result<OrderResponse>>;

// GetOrderByIdQueryHandler.cs
internal sealed class GetOrderByIdQueryHandler(HrastDbContext dbContext)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        var order = await dbContext.Set<Order>()
            .AsNoTracking()
            .Where(o => o.Id == query.Id)
            .Select(o => new OrderResponse(o.Id, o.Quantity))
            .FirstOrDefaultAsync(ct);

        return order is not null ? order : OrderErrors.NotFound;
    }
}
```

Use `AsNoTracking()` for read-only queries and project straight into the `*Response`. Never name a type `*Dto`.

---

## FluentValidation Validator

Place next to the command. Auto-discovered via `AddValidatorsFromAssembly(assembly, includeInternalTypes: true)` — no manual registration. Validators are `internal sealed`, so the `includeInternalTypes: true` flag is required (FluentValidation skips internal types by default):

```csharp
internal sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
```

`ValidationBehavior` runs all validators before the handler. If any fail, the handler never executes and HTTP 422 is returned automatically.

---

## Controller Wiring

Controllers live in `src/HrastERP.API/Controllers/`, not in the module. Use `ISender` to dispatch and `ToActionResult()` to map. Bind the command directly as the body; add a `*Request` (in `HrastERP.API/Requests/`) only when no command exists or the body differs from the command. Every action declares its one success status and its error statuses with `[ProducesResponseType]` (`ResponseTypeDeclarationTests` enforces it):

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController(ISender sender) : ControllerBase
{
    // Result<T> → 200 OK + { "data": T }. {id:guid} is required: ToCreatedResult points at this route.
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.FinanceView)]
    [ProducesResponseType<ApiResponse<OrderResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await sender.Send(new GetOrderByIdQuery(id), ct)).ToActionResult();

    // 201 Created + Location header + { "data": id }
    [HttpPost]
    [RequirePermission(Permission.FinanceCreate)]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        // The result of a create is the id (Guid) of the newly created order; ToCreatedResult uses
        // result.Value to build the Location header pointing at GetById for that id
        return result.ToCreatedResult(nameof(GetById));
    }

    // Result (no value) → 204 No Content
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.FinanceDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await sender.Send(new DeleteOrderCommand(id), ct)).ToActionResult();
}
```

---

## Pipeline Behaviors (automatic)

No extra code needed — these run on every handler:

| Behavior | What it does |
|---|---|
| `LoggingBehavior` | Logs request name and elapsed time |
| `TenantValidationBehavior` | Returns 403 if `ICurrentTenant.TenantId` is empty |
| `ValidationBehavior` | Runs FluentValidation validators; returns 422 on failure |

---

## Common Mistakes

| Mistake | Correct |
|---|---|
| Putting controller in module project | Controllers go in `HrastERP.API/Controllers/` |
| Using `IMediator` instead of `ISender` | Use `ISender` — only dispatch is needed |
| Throwing exceptions for expected failures | Return `Result.Failure(error)` |
| Manual validator registration | `AddValidatorsFromAssembly` in `Add<Module>Module()` handles it |
| Forgetting `[RequirePermission]` | Add to every endpoint that needs authorization |
| Branching on `IsSuccess` to pick a success status | Use `ToCreatedResult` (201) or `ToActionResult` (200 / 204) |
| Missing `[ProducesResponseType]` on an action | Declare its one success status and its error statuses (`ErrorResponse`), see `docs/api/api-responses.md` |
| Naming a returned type `*Dto` or putting it outside `Models/` | `*Response` in `Application/<Feature>/Models/` |
| A `*Request` that maps 1:1 onto the command | Bind the command directly |
| `public` validator or handler | `internal sealed` (validators need `includeInternalTypes: true`, already set) |
| `return BadRequest()` when route id and body id differ | `return Result.Failure(ApiErrors.RouteIdMismatch).ToActionResult();` (422) |
