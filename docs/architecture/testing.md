# Testing

## Stack

| Package | Role |
|---|---|
| **xUnit** | Test runner. `xunit` is a global using in all test projects — no `using Xunit;` needed. |
| **FluentAssertions** | Assertion library. Always use `.Should()` API — never use `Assert.*`. |
| **NSubstitute** | Mocking library. Used to create test doubles for interfaces. |
| **Testcontainers.PostgreSql** | Spins up a real PostgreSQL container for integration tests. |
| **Testcontainers.Redis** | Spins up a real Redis container for integration tests. |

Each module has a dedicated test project under `tests/` (e.g. `tests/HrastERP.Infrastructure.Tests/`, `tests/HrastERP.SharedKernel.Tests/`).

---

## Test types

### Unit tests

Test a single class in isolation. All external dependencies (interfaces like `ICurrentUser`, `ICurrentTenant`, `IRecurringJobManager`) are replaced with NSubstitute substitutes.

- No database, no network, no Docker required
- Fast — run in milliseconds
- Examples: settings validation, pipeline behaviors, domain entity logic, service method logic

**Do NOT use NSubstitute** for concrete test-infrastructure types — `TestDbContext`, `TestEntity`, test data records, or any class that is a concrete subclass of an abstract base class. Those are not dependency doubles; keep them as-is.

### Integration tests

Test behavior against real infrastructure — a live PostgreSQL instance or a live Redis instance. These tests verify interceptors, query filters, authentication flows, and cache operations where the real storage behavior matters.

- Require Docker to be running
- Use Testcontainers to spin up isolated containers
- Tagged with `[Trait("Category", "Integration")]` so they can be filtered

---

## Testcontainers vs InMemory

### Use Testcontainers (real database) for

- **EF Core interceptor tests** — `AuditableEntityInterceptorTests`, `SoftDeleteInterceptorTests`, `TenantEntityInterceptorTests`, `AuditLogInterceptorTests`. Interceptors depend on EF Core's change tracking pipeline, which InMemory does not fully replicate.
- **EF Core query filter tests** — `SoftDeleteQueryFilterTests`, `TenantQueryFilterTests`. Global query filters must be verified against a real SQL engine.
- **`AuthServiceTests`** — the full login/refresh/logout flow writes refresh tokens to the database and requires real migrations.
- **`RedisCacheServiceTests`** — prefix-based key removal (`RemoveByPrefixAsync`) requires Redis SCAN commands unavailable in InMemory.

### Use InMemory for

Tests that do not exercise SQL-specific behavior — cleanup job property tests, pipeline behavior logic, settings validation, pure service logic. These run fast and need no external dependencies.

---

## Fixtures

Shared container fixtures live in `tests/HrastERP.Infrastructure.Tests/Fixtures/`.

**`PostgresContainerFixture`** — starts one PostgreSQL 16 container per test class (`IClassFixture<PostgresContainerFixture>`).

- `NewConnectionString()` returns a unique connection string pointing to a fresh database named `test_{Guid:N}`. Call this once per test (typically inside a `CreateContext()` helper or at the top of the test body) so each test gets its own fully isolated database with no shared state.
- Tests using a custom `TestDbContext` call `context.Database.EnsureCreated()` to build the schema.
- Tests using the real `HrastDbContext` call `await _dbContext.Database.MigrateAsync()` inside `IAsyncLifetime.InitializeAsync()`.

**`RedisContainerFixture`** — starts one Redis 7 container per test class.

- `ConnectionString` exposes the container's connection string.
- Connect a `ConnectionMultiplexer` and create a `RedisCache` inside `IAsyncLifetime.InitializeAsync()` (constructor cannot be async).

---

## Naming convention

All test methods follow the pattern:

```
ExpectedBehavior_When_Condition
```

- `When` is always capitalized
- State the expected outcome first, then the condition under which it holds
- No `Test` prefix, no `Should` prefix

**Examples:**

```
Returns_success_When_credentials_are_valid
Returns_invalid_credentials_When_email_is_not_found
Throws_InvalidOperationException_When_TenantId_is_empty_on_insert
Sets_CreatedAt_and_CreatedBy_When_entity_is_added
Removes_all_matching_keys_When_prefix_matches
Has_expected_default_values_When_instantiated
Fails_validation_When_required_field_is_missing
```

---

## Running tests

```bash
# Fast tests only — no Docker required
dotnet test --filter "Category!=Integration"

# All tests — requires Docker running
dotnet test

# Integration tests only
dotnet test --filter "Category=Integration"

# Single test project
dotnet test tests/HrastERP.Infrastructure.Tests/

# Single test by name
dotnet test --filter "FullyQualifiedName~Returns_success_When_credentials_are_valid"
```
