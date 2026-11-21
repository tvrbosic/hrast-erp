# API Documentation & Health Checks

## OpenAPI with Scalar UI

Interactive API documentation using [Scalar](https://scalar.com/) as the UI and the built-in `Microsoft.AspNetCore.OpenApi` package for document generation.

### Availability

Only when `OpenApi:Enabled` is true (`OpenApiSettings`, default false; on in `appsettings.Development.json`). Otherwise the OpenAPI JSON endpoint and Scalar UI are not registered.

### URLs

| URL | Purpose |
|---|---|
| `/openapi/v1.json` | Raw OpenAPI document (JSON) |
| `/scalar/v1` | Interactive Scalar API reference UI |

### JWT Authentication in Scalar

The OpenAPI document includes a global `BearerAuth` security scheme. In Scalar UI:

1. Click the "Auth" button in the top bar
2. Enter the JWT token obtained from `POST /api/auth/login`
3. All subsequent requests from Scalar will include the `Authorization: Bearer <token>` header

### Configuration

Registration is in `HrastERP.API/OpenApi/OpenApiServiceExtensions.cs`:

- `AddOpenApiServices()` — registers OpenAPI document generation with a document transformer that sets the API title, version, and JWT security scheme
- `UseOpenApiInfrastructure()` — maps the OpenAPI and Scalar endpoints; no-ops when `OpenApi:Enabled` is false

### Customizing the OpenAPI Document

To add metadata or modify the document, edit the document transformer lambda inside `AddOpenApiServices()`. The transformer receives the `OpenApiDocument` object where you can modify `Info`, `Components`, `Security`, etc.

---

## Health Checks

`GET /health` returns a JSON report with per-dependency connectivity status.

### Availability

All environments. The endpoint is unauthenticated (`.AllowAnonymous()`) so it can be reached by load balancers and monitoring tools.

### Response Format

**HTTP 200** for Healthy or Degraded, **HTTP 503** for Unhealthy.

```json
{
  "status": "Healthy",
  "checks": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.023"
    },
    "redis": {
      "status": "Healthy",
      "duration": "00:00:00.005"
    },
    "smtp": {
      "status": "Unhealthy",
      "duration": "00:00:01.002",
      "description": "Connection refused"
    }
  },
  "totalDuration": "00:00:01.030"
}
```

- `description` is only present when the check is not Healthy
- Duration is formatted as `hh:mm:ss.fff`

### Registered Checks

| Name | What it checks | Package / Implementation |
|---|---|---|
| `database` | PostgreSQL connectivity | `AspNetCore.HealthChecks.NpgSql` |
| `redis` | Redis connectivity | `AspNetCore.HealthChecks.Redis` |
| `smtp` | SMTP server TCP connectivity | Custom `SmtpHealthCheck` (3-second timeout) |

### Adding a New Health Check

1. If using a community package, add the NuGet package to `HrastERP.API.csproj`
2. For custom checks, create a class implementing `IHealthCheck` in `HrastERP.API/HealthChecks/`
3. Register the check in `HealthCheckServiceExtensions.AddHealthCheckServices()`:

```csharp
services
    .AddHealthChecks()
    // ... existing checks
    .AddCheck<MyNewHealthCheck>("my-check");
```

The new check will automatically appear in the `/health` response.

### Configuration

Registration is in `HrastERP.API/HealthChecks/HealthCheckServiceExtensions.cs`:

- `AddHealthCheckServices()` — registers all health checks; the database check takes its connection string from `DatabaseSettings`, and the redis check reuses the shared `IConnectionMultiplexer` from the cache services
- `UseHealthCheckEndpoint()` — maps `GET /health` with the custom JSON response writer

The JSON response writer is in `HrastERP.API/HealthChecks/HealthCheckResponseWriter.cs`.

## CORS

Browser clients on another origin are allowed through a single default CORS policy. It is configured by `CorsSettings` (section `Cors`) and registered by `AddApiCors()` in `HrastERP.API/Cors/CorsServiceExtensions.cs`. `Program.cs` adds the middleware with `app.UseCors()`.

| Setting | Type | Default | Description |
|---|---|---|---|
| `AllowedOrigins` | string[] | `[]` | Origins allowed to call the API; empty means no cross-origin access |
| `PreflightMaxAgeMinutes` | int (0–1440) | 10 | How long browsers may cache a preflight response |

Each origin must be exactly `scheme://host[:port]` as the browser sends it in the `Origin` header: `http` or `https`, with no trailing slash, path, query, user info, explicit default port, surrounding whitespace or `*`. Anything else fails at startup, because it would never match.

The policy allows any header and method, exposes `Location` (so clients can read it from `201` responses), and does not allow credentials, since the API uses bearer tokens, not cookies.

`app.UseCors()` runs after `UseLoggingInfrastructure()` and before the Hangfire dashboard and `UseAuthentication()`, so preflights are answered before authentication. It sits inside `GlobalExceptionMiddleware`, which keeps headers, so browsers can also read `500` responses.

`appsettings.Development.json` allows `http://localhost:5173` as a placeholder for a local frontend dev server.
