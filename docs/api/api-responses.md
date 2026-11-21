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

Validation responses (422) additionally include an `errors` field with per-field messages. The `errors` key is omitted entirely from non-validation responses (401, 403, 404, 409, 500).

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
- For `Result<T>`: `200 OK` with the value wrapped in an API envelope `{ "data": ... }` on success; for `PagedResult<T>`, the envelope also includes `"meta"` with pagination info
- For a non-generic `Result`: `204 No Content` with no body on success
- The appropriate error status code (401/403/404/409/422/500) with `{ code, message }` body on failure; validation (422) responses also include `errors` with field-level messages

### Choosing the success status

| Handler returns | Endpoint creates an addressable resource? | Controller calls | Status |
|---|---|---|---|
| `Result<Guid>` (the new id) | yes (has `GET …/{id}`) | `ToCreatedResult(nameof(GetById))` | 201 |
| `Result<T>` | no | `ToActionResult()` | 200 |
| `Result` | — | `ToActionResult()` | 204 |

```csharp
[HttpPost]
public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken ct)
{
    // The result of a create is the id (Guid) of the newly created order; ToCreatedResult uses
    // result.Value to build the Location header pointing at GetById for that id
    var result = await sender.Send(command, ct);
    return result.ToCreatedResult(nameof(GetById));
}
```

The body is `{ "data": "<id>" }` and the `Location` header points at `GetById`, with the new id passed as its `id` route value. ASP.NET Core builds that URL when the response is written, so `GetById`'s route must have an `{id}` parameter (`{id:guid}`); a mismatch fails the request with a 500 after the row is saved. `CreatedLocationRouteTests` checks this for every controller that creates resources.

Never branch on `result.IsSuccess` to pick a success status; add an extension method to `ResultExtensions` instead.

### Route id and body id must match

`PUT` / `PATCH` actions whose command carries an `Id` check it against the `{id}` in the URL before sending the command. A mismatch returns `ApiErrors.RouteIdMismatch` (`src/HrastERP.API/Responses/ApiErrors.cs`) through `ToActionResult()`: `422` `General.Validation` with a field error on `id`, the same shape as any other validation failure. Never return a bare `BadRequest()`; it produces a `ProblemDetails` body, not `ErrorResponse`.

```csharp
if (id != command.Id)
    return Result.Failure(ApiErrors.RouteIdMismatch).ToActionResult();
```

### Declaring responses for OpenAPI

Actions return `IActionResult`, so OpenAPI (and the Scalar UI) only knows their responses from `[ProducesResponseType]`. Every action declares:

- exactly one success response, matching the table above: `[ProducesResponseType<ApiResponse<T>>(StatusCodes.Status200OK)]` (paged lists use `ApiResponse<IReadOnlyCollection<TItem>>`), `[ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]` or `[ProducesResponseType(StatusCodes.Status204NoContent)]`;
- each error status the endpoint can return, typed `ErrorResponse`: `401` when the endpoint needs a token or its handler returns an `Unauthorized` error, `403` for any `[RequirePermission]` endpoint, `404` / `409` for the handler's `NotFound` / `Conflict` errors, and `422` when the action takes a body or query parameters or its handler returns a `Validation` error.

`500` is not declared per action; `GlobalExceptionMiddleware` can return it from any endpoint. A 403 from a failed policy (`[RequirePermission]`, `[RequireSuperAdminTenant]`) is written by `ForbiddenResponseHandler` as an `ErrorResponse` — `General.Forbidden`, or the policy's own code such as `Tenant.SuperAdminRequired`. A 401 for a missing, invalid or expired access token is written by `JwtChallengeResponse` (JwtBearer `OnChallenge`) as an `ErrorResponse` — `Auth.TokenExpired` when the token has expired (the client should call `POST /api/auth/refresh`), otherwise `General.Unauthorized` — with a `WWW-Authenticate: Bearer` header. Login and refresh return their own 401 (`Auth.InvalidCredentials`, `Auth.InvalidRefreshToken`); a client must not try to refresh when `/refresh` itself returns 401.

`ResponseTypeDeclarationTests` checks the success declarations and that every declared error uses `ErrorResponse`.

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

`ModelBindingExtensions.ConfigureModelBindingErrorFormat()` (in `HrastERP.API/Responses/`) replaces the default `InvalidModelStateResponseFactory` so that model binding errors produce the same response format as `ValidationBehavior`:

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

---

## Framework Error Responses

Some errors are produced by ASP.NET Core itself, before any controller runs, with no body. `StatusCodeErrorResponse` (`HrastERP.API/Middleware/`, registered with `UseStatusCodePages` right after `GlobalExceptionMiddleware`) gives them an `ErrorResponse`:

| Status | When | Code |
|---|---|---|
| 404 | No route matches the URL (typo, or an `{id:guid}` that isn't a GUID) | `General.RouteNotFound` |
| 405 | The URL exists but not for this HTTP method (the `Allow` header lists the supported ones) | `General.MethodNotAllowed` |
| 415 | The body's `Content-Type` isn't JSON | `General.UnsupportedMediaType` |

ASP.NET Core only calls the handler for an error status with no body, so responses our code writes, such as a handler's 404 `Tenant.NotFound`, are never touched. `ConfigureModelBindingErrorFormat()` also sets `SuppressMapClientErrors = true`; otherwise `[ApiController]` would turn the 415 into `ProblemDetails` before the handler sees it.
