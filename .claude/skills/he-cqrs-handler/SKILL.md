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
```

```csharp
// CreateOrderCommand.cs
public record CreateOrderCommand(Guid CustomerId, int Quantity) : IRequest<Result<Guid>>;

// CreateOrderCommandHandler.cs
public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly HrastDbContext _db;

    public CreateOrderCommandHandler(HrastDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync([command.CustomerId], ct);
        if (customer is null)
            return OrderErrors.CustomerNotFound;  // implicit conversion to Result<Guid>.Failure

        var order = Order.Create(Guid.NewGuid(), command.CustomerId, command.Quantity);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        return order.Id;  // implicit conversion to Result<Guid>.Success
    }
}
```

---

## Query Structure

```csharp
public record GetOrderQuery(Guid Id) : IRequest<Result<OrderDto>>;

public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery query, CancellationToken ct)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == query.Id, ct);

        if (order is null)
            return OrderErrors.NotFound;

        return new OrderDto(order.Id, order.Quantity);
    }
}
```

Use `AsNoTracking()` for read-only queries.

---

## FluentValidation Validator

Place next to the command. Auto-discovered via `AddValidatorsFromAssembly(assembly, includeInternalTypes: true)` — no manual registration. Validators are `internal sealed`, so the `includeInternalTypes: true` flag is required (FluentValidation skips internal types by default):

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

`ValidationBehavior` runs all validators before the handler. If any fail, the handler never executes and HTTP 422 is returned automatically.

---

## Controller Wiring

Controllers live in `src/HrastERP.API/Controllers/`, not in the module. Use `ISender` to dispatch and `ToActionResult()` to map:

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController(ISender sender) : ControllerBase
{
    // Default 200 OK
    [HttpGet("{id}")]
    [RequirePermission(Permission.FinanceView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await sender.Send(new GetOrderQuery(id), ct)).ToActionResult();

    // 201 Created + Location header + { "data": id }
    [HttpPost]
    [RequirePermission(Permission.FinanceCreate)]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateOrderCommand(request.CustomerId, request.Quantity), ct);
        // The result of a create is the id (Guid) of the newly created order; ToCreatedResult uses
        // result.Value to build the Location header pointing at Get for that id
        return result.ToCreatedResult(nameof(Get));
    }

    // Result (no value) → 204 No Content
    [HttpDelete("{id}")]
    [RequirePermission(Permission.FinanceDelete)]
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
| `return BadRequest()` when route id and body id differ | `return Result.Failure(ApiErrors.RouteIdMismatch).ToActionResult();` (422) |
