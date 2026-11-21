# Logging

Structured logging via Serilog. Replaces the default ASP.NET Core logging provider — all `ILogger<T>` injections automatically route through Serilog.

## Sinks

| Sink | Format | Purpose |
|------|--------|---------|
| Console | Human-readable, colored | Terminal output during development and runtime |
| Debug | Default | Visual Studio / Rider debugger output window (Development only) |
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

Everything comes from the `"Serilog"` section of `appsettings.json`: minimum levels, enrichers and sinks. `AddLoggingInfrastructure()` only calls `ReadFrom.Configuration`. Sink and enricher assemblies are found automatically from the package references, so there is no `Using` list.

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
    "Enrich": [ "FromLogContext", "WithMachineName", "WithEnvironmentName" ],
    "WriteTo": {
      "Console": { "Name": "Console" },
      "File": {
        "Name": "File",
        "Args": {
          "path": "./logs/hrast-erp-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "formatter": "Serilog.Formatting.Json.JsonFormatter, Serilog"
        }
      }
    }
  }
}
```

`WriteTo` uses named keys instead of an array, because configuration merges arrays by index. With named keys, `appsettings.Development.json` can add a sink (it adds `"Debug": { "Name": "Debug" }`) and an environment variable can change one sink's argument:

```
Serilog__WriteTo__File__Args__path=/var/log/hrast/hrast-erp-.log
```

Environment variables cannot delete a key, so removing a base sink (e.g. File in a container) needs an environment-specific `appsettings.{Environment}.json`.

Development overrides in `appsettings.Development.json` also relax EF Core and ASP.NET Core to `Information` and set default level to `Debug`.

**Migrating from the old keys:** `Serilog__File__Path` and `Serilog__File__RetentionDays` are no longer read, and nothing reports them. Use `Serilog__WriteTo__File__Args__path` and `Serilog__WriteTo__File__Args__retainedFileCountLimit` instead.

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
2. Add a named entry under `Serilog:WriteTo` in `appsettings.json`, e.g. `"Seq": { "Name": "Seq", "Args": { "serverUrl": "http://localhost:5341" } }`. No C# change is needed.
