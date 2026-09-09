# Caching

Redis-based distributed caching with a typed `ICacheService` abstraction. Provides JSON serialization, configurable TTLs, and prefix-based bulk invalidation via Redis SCAN.

## Configuration

Settings in `appsettings.json` under `"Cache"`:

```json
{
  "Cache": {
    "ConnectionString": "localhost:6379",
    "DefaultTtlMinutes": 60
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `ConnectionString` | string | — | Redis connection string (required) |
| `DefaultTtlMinutes` | int (1-1440) | 60 | Default cache entry TTL in minutes |

Bound to `CacheSettings` via `ValidateDataAnnotations()` + `ValidateOnStart()` — misconfiguration fails at startup.

## Development Setup

Redis runs via Docker Compose:

```bash
docker compose up -d redis
```

Connection string in `appsettings.Development.json`: `"localhost:6379"`. No password for local dev (matches the Postgres pattern).

## Using the Service

Inject `ICacheService` into your handler. All operations are non-throwing — cache failures are logged and return null/silently complete.

### Get

```csharp
var roles = await cacheService.GetAsync<List<RoleDto>>("Roles:all", ct);
if (roles is null)
{
    roles = await repository.GetAllAsync(ct);
    await cacheService.SetAsync("Roles:all", roles, cancellationToken: ct);
}
return roles;
```

Returns `null` on cache miss, expiration, or deserialization failure.

### Set

```csharp
await cacheService.SetAsync("Roles:all", roles, cancellationToken: ct);
```

Serializes to JSON. Uses `DefaultTtlMinutes` when no explicit expiration is given.

### Set with Custom TTL

```csharp
await cacheService.SetAsync("Roles:all", roles, TimeSpan.FromMinutes(30), ct);
```

### Remove

```csharp
await cacheService.RemoveAsync("Roles:all", ct);
```

### Remove by Prefix

```csharp
await cacheService.RemoveByPrefixAsync("Roles", ct);
```

Removes all keys matching `Roles:*`. Uses Redis SCAN (non-blocking).

## Cache Key Conventions

Recommended pattern: `Module:Entity:Identifier`

| Key Pattern | Example | Use Case |
|---|---|---|
| `{Entity}:all` | `Roles:all` | Full list cache |
| `{Entity}:{id}` | `Roles:00000000-...` | Single entity by ID |
| `{Entity}:list:{params}` | `Suppliers:list:page-1-size-20` | Paged list with parameters |
| `{Module}:{Entity}:{id}` | `Admin:Tenants:abc123` | Module-scoped entity |

## Invalidation Pattern

Invalidate cache entries in command handlers after successful writes:

```csharp
internal sealed class UpdateRoleHandler(
    IRoleRepository repository,
    ICacheService cacheService) : IRequestHandler<UpdateRoleCommand, Result>
{
    public async Task<Result> Handle(UpdateRoleCommand command, CancellationToken ct)
    {
        // ... update logic ...

        await cacheService.RemoveAsync($"Roles:{command.RoleId}", ct);
        await cacheService.RemoveByPrefixAsync("Roles", ct);

        return Result.Success();
    }
}
```

## Error Codes

| Code | Type | When |
|---|---|---|
| `Cache.SerializationFailed` | Unexpected | JSON serialization or deserialization failed |
| `Cache.OperationFailed` | Unexpected | A Redis operation failed |

Cache errors are non-fatal — they are logged as warnings but never thrown to callers.
