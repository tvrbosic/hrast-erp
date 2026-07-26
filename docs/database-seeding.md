# Database Seeding

## Overview

`DatabaseSeeder` (`src/HrastERP.Infrastructure/Database/DatabaseSeeder.cs`) applies plain SQL scripts at application startup. It runs before the middleware pipeline via a scoped DI block in `Program.cs`. EF Core migrations must be applied before the seeder runs.

## Seed Script Locations

```
src/HrastERP.Infrastructure/Database/Seeds/
├── Reference/    # Always applied — every environment
└── Fixtures/     # Applied only in Development environment
```

Reference scripts run first, then Fixtures (in Development). Within each folder, scripts execute in ascending numeric order.

## Naming Convention

```
NNNN_description.sql
```

- `NNNN` — 4-digit zero-padded integer prefix (e.g. `0001`, `0002`, `0010`)
- `description` — kebab-case description of what the script seeds
- Examples: `0001_roles.sql`, `0002_admin_user.sql`

The prefix is parsed as an integer, so `0009_x.sql` always runs before `0010_x.sql` regardless of lexicographic order.

## Idempotency

Scripts are safe to re-run because:

1. **`seed_history` table** — the seeder records every applied script by filename. On subsequent startups, already-applied scripts are skipped.
2. **SQL guards** — scripts use `INSERT ... ON CONFLICT ("Id") DO NOTHING` so re-execution is a no-op at the database level.

The `seed_history` table is created automatically by the seeder. It is not managed by EF Core migrations.

## Adding a New Seed Script

1. Determine the correct folder: `Reference/` for data that must exist in all environments; `Fixtures/` for dev-only data.
2. Choose the next available prefix number (check existing files in the folder).
3. Create `NNNN_description.sql` with idempotent SQL using `ON CONFLICT ("Id") DO NOTHING`.
4. The script will be copied to the build output automatically (configured via `<Content CopyToOutputDirectory="PreserveNewest">` in `HrastERP.Infrastructure.csproj`).
5. The next application startup will apply it and record it in `seed_history`.

### Example Reference Script

```sql
INSERT INTO "SomeTable" ("Id", "Name")
VALUES
    ('00000000-0000-0000-0000-000000000001', 'Default Value')
ON CONFLICT ("Id") DO NOTHING;
```

### Example Fixture Script

```sql
-- For development use only
INSERT INTO "AspNetUsers" ("Id", "UserName", ...)
VALUES ('...', 'devuser', ...)
ON CONFLICT ("Id") DO NOTHING;
```

## Predefined Role UUIDs

These are stable across all environments and used as FK targets:

| Role | UUID |
|---|---|
| Administrator | `00000000-0000-0000-0000-000000000001` |
| ProcurementOperator | `00000000-0000-0000-0000-000000000002` |
| ProductionWorker | `00000000-0000-0000-0000-000000000003` |
| WarehouseEmployee | `00000000-0000-0000-0000-000000000004` |
| FinanceEmployee | `00000000-0000-0000-0000-000000000005` |

## Resetting Seed Data

Drop and recreate the database, re-apply migrations, then restart the app. The seeder will re-apply all scripts from scratch.

## Notes

- `ON CONFLICT DO NOTHING` means existing rows are not updated. To update reference data (e.g. change a role's permissions), add a new script with an `UPDATE` statement — do not modify existing scripts that have already run against production databases.
- The dev admin user fixture (`Database/Seeds/Fixtures/0001_dev_admin_user.sql`) uses `TenantId = Guid.Empty` as a placeholder until the Tenant entity is introduced in Phase 1.
