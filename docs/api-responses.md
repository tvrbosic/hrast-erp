# API Response Conventions

This document describes the HTTP response formats produced by the Hrast ERP API — both success and error shapes — and how application `Result` values are mapped to HTTP responses.

---

## Success Responses

### Standard envelope

All successful responses from `ToActionResult()` are wrapped in a standard envelope:

```json
{
    "data": { "id": "...", "name": "..." }
}
```

### Paged responses

Paged responses include pagination metadata:

```json
{
    "data": [ ... ],
    "meta": {
        "page": 1,
        "pageSize": 10,
        "totalCount": 42,
        "totalPages": 5,
        "hasPreviousPage": false,
        "hasNextPage": true
    }
}
```

---

## Error Responses

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
- `200 OK` with the value wrapped in an API envelope `{ "data": ... }` on success; for `PagedResult<T>`, the envelope also includes `"meta"` with pagination info
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

---

## Global Exception Middleware

`GlobalExceptionMiddleware` (registered first in `Program.cs`) is a safety net for unhandled infrastructure or framework exceptions — database connectivity failures, unexpected nulls, framework bugs. It logs the exception and returns:

```json
{
    "code": "General.Unexpected",
    "message": "An unexpected error occurred."
}
```

This middleware is **not** the primary error path. Application-layer failures always use `Result.Failure` with an appropriate `ErrorType`. The middleware only fires if something truly unexpected escapes the handler pipeline.

---

## Model Binding Error Factory

When ASP.NET Core fails to deserialize or bind the request body (malformed JSON, missing `[Required]` fields, wrong types), it short-circuits the pipeline **before** MediatR runs. By default, ASP.NET returns its own `ValidationProblemDetails` format which differs from the application's `ErrorResponse` shape.

`ModelBindingExtensions.ConfigureModelBindingErrorFormat()` (in `HrastERP.API/Extensions/`) replaces the default `InvalidModelStateResponseFactory` so that model binding errors produce the same response format as `ValidationBehavior`:

```json
{
    "code": "General.Validation",
    "message": "One or more validation errors occurred.",
    "errors": {
        "email": ["The Email field is required."],
        "password": ["The Password field is required."]
    }
}
```

Field names are converted to camelCase to match `ValidationBehavior` output. The response status code is 422 Unprocessable Entity.
