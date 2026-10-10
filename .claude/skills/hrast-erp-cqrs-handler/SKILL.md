---
name: hrast-erp-cqrs-handler
description: Guides writing MediatR command and query handlers in HrastERP — handler structure, FluentValidation validator placement, controller wiring with ISender, ToActionResult() mapping, and custom status codes (201/204). Use when implementing a new command or query.
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

Place next to the command. Auto-discovered via `AddValidatorsFromAssembly` — no manual registration:

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

    // Custom 201 Created
    [HttpPost]
    [RequirePermission(Permission.FinanceCreate)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateOrderCommand(request.CustomerId, request.Quantity), ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value)
            : result.ToActionResult();
    }

    // 204 No Content
    [HttpDelete("{id}")]
    [RequirePermission(Permission.FinanceDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteOrderCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
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
| Using `ToActionResult()` for 201/204 | Handle `result.IsSuccess` branch manually for non-200 responses |
