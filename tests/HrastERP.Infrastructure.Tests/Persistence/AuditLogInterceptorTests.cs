using System.Text.Json;
using FluentAssertions;
using HrastERP.Infrastructure.Persistence;
using HrastERP.Infrastructure.Persistence.Audit;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Persistence;

public class AuditLogInterceptorTests : IDisposable
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id)
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class FakeCurrentUser(Guid userId, Guid tenantId, bool isAuthenticated) : ICurrentUser
    {
        public Guid UserId => userId;
        public Guid TenantId => tenantId;
        public string Username => "testuser";
        public bool IsAuthenticated => isAuthenticated;
        public Permission EffectivePermissions => Permission.None;
    }

    private sealed class FakeCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();
        public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Name).HasMaxLength(200);
            });

            modelBuilder.Entity<AuditLogEntry>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Action).HasConversion<string>();
            });
        }
    }

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private TestDbContext CreateContext(bool isAuthenticated = true)
    {
        var currentUser = new FakeCurrentUser(_userId, _tenantId, isAuthenticated);
        var currentTenant = new FakeCurrentTenant(_tenantId);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(
                new AuditableEntityInterceptor(currentUser),
                new SoftDeleteInterceptor(currentUser),
                new TenantEntityInterceptor(currentTenant),
                new AuditLogInterceptor(currentUser, currentTenant))
            .Options;

        return new TestDbContext(options);
    }

    // --- Created entity tests ---

    [Fact]
    public async Task Created_entity_produces_audit_log_with_Created_action()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var logs = await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync();
        logs.Should().ContainSingle();
        logs[0].Action.Should().Be(AuditAction.Created);
    }

    [Fact]
    public async Task Created_entity_audit_log_has_correct_entity_name_and_id()
    {
        await using var context = CreateContext();
        var entityId = Guid.NewGuid();
        var entity = new TestEntity(entityId) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.EntityName.Should().Be(nameof(TestEntity));
        log.EntityId.Should().Be(entityId.ToString());
    }

    [Fact]
    public async Task Created_entity_audit_log_has_null_OldValues_and_all_properties_in_NewValues()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.OldValues.Should().BeNull();
        log.NewValues.Should().NotBeNullOrEmpty();

        var newValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.NewValues!);
        newValues.Should().ContainKey("Name");
        newValues!["Name"].GetString().Should().Be("Widget");
    }

    [Fact]
    public async Task Created_entity_audit_log_has_correct_user_id()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Created_entity_audit_log_has_correct_tenant_id()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Created_entity_audit_log_has_utc_timestamp()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        log.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    // --- Updated entity tests ---

    [Fact]
    public async Task Updated_entity_produces_audit_log_with_Updated_action()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.Name = "Updated Widget";
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync();

        var logs = await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync();
        logs.Should().HaveCount(2);
        logs[1].Action.Should().Be(AuditAction.Updated);
    }

    [Fact]
    public async Task Updated_entity_audit_log_captures_changed_properties_only()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.Name = "Updated Widget";
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync();

        var log = (await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync())[1];
        log.OldValues.Should().NotBeNullOrEmpty();
        log.NewValues.Should().NotBeNullOrEmpty();

        var oldValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.OldValues!);
        var newValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.NewValues!);

        oldValues.Should().ContainKey("Name");
        newValues.Should().ContainKey("Name");
        newValues!["Name"].GetString().Should().Be("Updated Widget");
    }

    // --- Soft-deleted entity tests ---

    [Fact]
    public async Task Soft_deleted_entity_produces_audit_log_with_Deleted_action()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        var logs = await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync();
        logs.Should().HaveCount(2);
        logs[1].Action.Should().Be(AuditAction.Deleted);
    }

    [Fact]
    public async Task Soft_deleted_entity_audit_log_has_populated_OldValues_and_null_NewValues()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        var log = (await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync())[1];
        log.OldValues.Should().NotBeNullOrEmpty();
        log.NewValues.Should().BeNull();

        var oldValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.OldValues!);
        oldValues.Should().ContainKey("Name");
    }

    // --- Edge case tests ---

    [Fact]
    public async Task Unauthenticated_user_sets_empty_guid_for_UserId()
    {
        await using var context = CreateContext(isAuthenticated: false);
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.UserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Audit_log_entry_itself_is_not_audit_logged()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var logs = await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync();
        logs.Should().ContainSingle();
    }

    [Fact]
    public async Task Each_audit_log_entry_has_unique_non_empty_id()
    {
        await using var context = CreateContext();
        context.TestEntities.Add(new TestEntity(Guid.NewGuid()) { Name = "A" });
        context.TestEntities.Add(new TestEntity(Guid.NewGuid()) { Name = "B" });
        await context.SaveChangesAsync();

        var logs = await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync();
        logs.Should().HaveCount(2);
        logs[0].Id.Should().NotBe(Guid.Empty);
        logs[0].Id.Should().NotBe(logs[1].Id);
    }

    public void Dispose()
    {
        // InMemory databases are disposed with the context
    }
}
