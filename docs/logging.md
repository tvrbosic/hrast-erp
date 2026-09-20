# Logging

Structured logging via Serilog. Replaces the default ASP.NET Core logging provider — all `ILogger<T>` injections automatically route through Serilog.

## Sinks

| Sink | Format | Purpose |
|------|--------|---------|
| Console | Human-readable, colored | Terminal output during development and runtime |
| Debug | Default | Visual Studio / Rider debugger output window |
| Rolling File | JSON | Machine-parsable logs at `./logs/hrast-erp-YYYY-MM-DD.log`, one file per day, 30-day retention |

## Enrichment

Every log entry is automatically enriched with:

| Property | Source |
|----------|--------|
| `RequestId` | `HttpContext.TraceIdentifier` (via `UseSerilogRequestLogging`) |
| `UserId` | `ICurrentUser.UserId` (via `LoggingEnrichmentMiddleware`) |
| `TenantId` | `ICurrentTenant.TenantId` (via `LoggingEnrichmentMiddleware`) |
| `MachineName` | `Serilog.Enrichers.Environment` |
| `EnvironmentName` | `Serilog.Enrichers.Environment` |
| `SourceContext` | `ILogger<T>` type parameter (automatic) |

For unauthenticated requests, `UserId` and `TenantId` are `Guid.Empty`.

## Configuration

Hybrid approach — structural setup in C#, tunables in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Hangfire": "Warning",
        "System.Net.Http": "Warning"
      }
    },
    "File": {
      "Path": "./logs/hrast-erp-.log",
      "RetentionDays": 30
    }
  }
}
```

Development overrides in `appsettings.Development.json` relax EF Core and ASP.NET Core to `Information` and set default level to `Debug`.

## Sensitive Data Masking

`LoggingBehavior` logs the full request payload for every MediatR command/query. To prevent sensitive data from appearing in logs, mark properties with `[SensitiveData]`:

```csharp
public record LoginCommand(
    string Email,
    [property: SensitiveData] string Password
) : IRequest<Result<LoginResponse>>;
```

Masked properties appear as `"***"` in log output. The attribute lives in `HrastERP.SharedKernel/Logging/`.

## Logging in Services

No changes needed — inject `ILogger<T>` as usual:

```csharp
public class MyService(ILogger<MyService> logger)
{
    public void DoWork()
    {
        logger.LogInformation("Processing {ItemId}", itemId);
    }
}
```

Serilog picks up all `ILogger<T>` calls automatically. Use structured message templates (e.g. `"Processing {ItemId}"`) — never string interpolation.

## Adding Future Sinks

To add an aggregation sink (e.g. Seq):

1. Add the NuGet package (`Serilog.Sinks.Seq`)
2. Add `.WriteTo.Seq(url)` in `LoggingServiceExtensions.AddLoggingInfrastructure()`
3. Add any connection settings to `appsettings.json`
