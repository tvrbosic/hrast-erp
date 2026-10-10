# Audit Log

## Overview

Append-only change history that automatically captures entity state changes (create, update, soft-delete) via the `AuditLogInterceptor` EF Core `SaveChangesInterceptor`.

## What Gets Audit-Logged

Any entity implementing `IAuditable` — which means all `BaseEntity<TId>` descendants. Entities that don't inherit `BaseEntity<TId>` (e.g., `Role`, `RefreshToken`, `ApplicationUser`, `AuditLogEntry` itself) are excluded automatically.

## AuditLogEntry

Plain class at `HrastERP.Infrastructure/Database/Audit/AuditLogEntry.cs`. Implements `ITenantEntity` for tenant-scoped query filtering. Does NOT inherit `BaseEntity<TId>`.

| Field | Type | Description |
|---|---|---|
| `Id` | `Guid` | PK, set by interceptor |
| `EntityName` | `string` | CLR type name of tracked entity |
| `EntityId` | `string` | Primary key of tracked entity |
| `Action` | `AuditAction` | `Created`, `Updated`, or `Deleted` |
| `OldValues` | `string?` | JSON snapshot of previous state |
| `NewValues` | `string?` | JSON snapshot of new state |
| `UserId` | `Guid` | From `ICurrentUser`; `Guid.Empty` if unauthenticated |
| `TenantId` | `Guid` | From `ICurrentTenant`; set explicitly by interceptor |
| `Timestamp` | `DateTime` | UTC timestamp |

## Value Capture Strategy

| Action | OldValues | NewValues |
|---|---|---|
| Created | `null` | All properties (full snapshot) |
| Updated | Changed properties only | Changed properties only |
| Deleted | All properties (full snapshot) | `null` |

## Interceptor Ordering

`AuditLogInterceptor` must be the **last** interceptor registered in `DatabaseServiceExtensions`:

1. `AuditableEntityInterceptor` — populates audit timestamp fields
2. `SoftDeleteInterceptor` — converts `Deleted` → `Modified` with `DeletedAt`/`DeletedBy`
3. `TenantEntityInterceptor` — populates `TenantId` on new entities
4. `AuditLogInterceptor` — captures final entity state

This ensures the interceptor sees all modifications applied by earlier interceptors.

## Soft-Delete Detection

`SoftDeleteInterceptor` converts `EntityState.Deleted` to `EntityState.Modified`. The `AuditLogInterceptor` detects this by checking if `DeletedAt` was modified and set to a non-null value:

```csharp
entry.State == EntityState.Modified
    && entry.Properties.Any(p =>
        p.Metadata.Name == nameof(ISoftDeletable.DeletedAt)
        && p.IsModified
        && p.CurrentValue is not null);
```

## TenantId on AuditLogEntry

`AuditLogInterceptor` sets `TenantId` explicitly from `ICurrentTenant` because `TenantEntityInterceptor` has already completed by the time audit log entries are added to the context.

## Database

- Table: `audit_log`
- Indexes: `IX_audit_log_entity` (EntityName, EntityId), `IX_audit_log_user` (UserId), `IX_audit_log_timestamp` (Timestamp)

## TODO — Phase 6.1: Audit Log API

- [ ] `GET /admin/audit-log` endpoint — filterable by entity type, entity ID, user, date range
- [ ] Paged list of `AuditLogEntry` records using `PagedResult<T>`
- [ ] Accessible only to Administrator role (`[RequirePermission(Permission.AdminView)]` or similar)
- [ ] MediatR query: `GetAuditLogQuery` with filters
