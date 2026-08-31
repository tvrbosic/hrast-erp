# Background Jobs

Hangfire provides background job processing with PostgreSQL storage. Jobs run in-process alongside the API — no separate worker service needed.

## Configuration

Settings in `appsettings.json` under `"Hangfire"`:

```json
{
  "Hangfire": {
    "WorkerCount": 1,
    "SoftDeleteRetentionDays": 90,
    "RevokedTokenRetentionDays": 7,
    "SoftDeleteCleanupCron": "0 2 * * *",
    "RefreshTokenCleanupCron": "0 2 * * *"
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `WorkerCount` | int (1–20) | 1 | Number of Hangfire worker threads |
| `SoftDeleteRetentionDays` | int (1–3650) | 90 | Days to keep soft-deleted entities before hard-delete |
| `RevokedTokenRetentionDays` | int (1–365) | 7 | Days to keep revoked refresh tokens before deletion |
| `SoftDeleteCleanupCron` | string | `"0 2 * * *"` | Cron schedule for soft-delete cleanup |
| `RefreshTokenCleanupCron` | string | `"0 2 * * *"` | Cron schedule for refresh token cleanup |

Bound to `HangfireSettings` via `ValidateDataAnnotations()` + `ValidateOnStart()` — misconfiguration fails at startup.

## Dashboard

The Hangfire dashboard is available at `/hangfire` and restricted to localhost connections via `LocalhostDashboardAuthorizationFilter`.

## Architecture

### IRecurringJobDefinition

Pure C# interface in `HrastERP.Infrastructure/Hangfire/Services/` — no Hangfire dependency:

```csharp
public interface IRecurringJobDefinition
{
    string JobId { get; }
    string CronExpression { get; }
    Task ExecuteAsync(CancellationToken cancellationToken);
}
```

### RecurringJobRegistrar

Called once at startup in `Program.cs`. Resolves all `IRecurringJobDefinition` implementations from DI, validates `JobId` uniqueness (throws `InvalidOperationException` on duplicates), and registers each with Hangfire.

### IBackgroundJobService

Thin abstraction over Hangfire's static APIs for ad-hoc job dispatch:

```csharp
// Fire-and-forget
_backgroundJobService.Enqueue<MyService>(x => x.DoWorkAsync());

// Delayed
_backgroundJobService.Schedule<MyService>(x => x.DoWorkAsync(), TimeSpan.FromMinutes(5));
```

## Built-in Cleanup Jobs

### RefreshTokenCleanupJob

- **JobId:** `infrastructure.refresh-token-cleanup`
- **Purpose:** Removes expired and revoked refresh tokens
- **Approach:** Batched raw SQL DELETE (100 per batch) — `RefreshToken` doesn't inherit `BaseEntity<TId>`, so no audit entries are written
- **Retention:** Expired tokens deleted immediately; revoked tokens kept for `RevokedTokenRetentionDays`

### SoftDeleteCleanupJob

- **JobId:** `infrastructure.soft-delete-cleanup`
- **Purpose:** Hard-deletes soft-deleted entities past their retention period
- **Approach:** Raw SQL to bypass the interceptor chain (no HTTP context in background jobs means no `ICurrentUser`/`ICurrentTenant`). Processes each `ISoftDeletable` entity type discovered from the EF model.
- **Audit:** Writes `Purged` audit entries with full entity snapshot in `OldValues` before deletion
- **Retention:** Entities with `DeletedAt` older than `SoftDeleteRetentionDays` are purged
- **Batching:** 100 entities per batch, each batch in its own transaction

## Adding a New Recurring Job

1. **Create the job class** implementing `IRecurringJobDefinition`:

```csharp
internal sealed class MyCleanupJob(
    HrastDbContext dbContext,
    IOptions<HangfireSettings> settings,
    ILogger<MyCleanupJob> logger) : IRecurringJobDefinition
{
    public string JobId => "module-name.my-cleanup";
    public string CronExpression => "0 3 * * *"; // or from settings

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Job logic here
    }
}
```

2. **Register in DI** — add to `BackgroundJobServiceExtensions` (for infrastructure jobs) or the module's DI registration:

```csharp
services.AddScoped<IRecurringJobDefinition, MyCleanupJob>();
services.AddScoped<MyCleanupJob>(); // Required for Hangfire's job activator
```

3. **Done** — `RecurringJobRegistrar` auto-discovers and registers the job at startup.

### Job ID Convention

Use dot-separated format: `<scope>.<action>`, e.g.:
- `infrastructure.refresh-token-cleanup`
- `infrastructure.soft-delete-cleanup`
- `inventory.stock-recount`

### Important: No HTTP Context

Background jobs run outside the HTTP request pipeline. This means:
- `ICurrentUser` and `ICurrentTenant` are not available (or resolve to empty/default values)
- EF Core interceptors (`AuditableEntityInterceptor`, `SoftDeleteInterceptor`, `TenantEntityInterceptor`, `AuditLogInterceptor`) will not behave correctly with tracked entities
- Use **raw SQL** for database operations that need to bypass the interceptor chain
- If audit entries are needed, insert them manually via raw SQL
