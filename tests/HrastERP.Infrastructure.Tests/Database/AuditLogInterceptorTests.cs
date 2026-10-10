using System.Text.Json;
using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Database.Audit;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class AuditLogInterceptorTests : IClassFixture<PostgresContainerFixture>
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id)
    {
        public string Name { get; set; } = string.Empty;
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

    private readonly PostgresContainerFixture _fixture;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public AuditLogInterceptorTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TestDbContext CreateContext(bool isAuthenticated = true)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(isAuthenticated);
        currentUser.UserId.Returns(isAuthenticated ? _userId : Guid.Empty);
        currentUser.TenantId.Returns(_tenantId);

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(_tenantId);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.NewConnectionString())
            .AddInterceptors(
                new AuditableEntityInterceptor(currentUser),
                new SoftDeleteInterceptor(currentUser),
                new TenantEntityInterceptor(currentTenant),
                new AuditLogInterceptor(currentUser, currentTenant))
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    // --- Created entity tests ---

    [Fact]
    public async Task Produces_audit_log_with_Created_action_When_entity_is_added()
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
    public async Task Records_correct_entity_name_and_id_When_entity_is_added()
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
    public async Task Records_null_OldValues_and_full_NewValues_snapshot_When_entity_is_added()
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
    public async Task Records_correct_user_id_When_entity_is_added()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Records_correct_tenant_id_When_entity_is_added()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Records_UTC_timestamp_When_entity_is_added()
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
    public async Task Produces_audit_log_with_Updated_action_When_entity_is_modified()
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
    public async Task Records_changed_properties_only_When_entity_is_modified()
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
    public async Task Produces_audit_log_with_Deleted_action_When_entity_is_soft_deleted()
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
    public async Task Records_OldValues_snapshot_and_null_NewValues_When_entity_is_soft_deleted()
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
    public async Task Records_empty_guid_for_UserId_When_user_is_unauthenticated()
    {
        await using var context = CreateContext(isAuthenticated: false);
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var log = await context.AuditLogEntries.IgnoreQueryFilters().SingleAsync();
        log.UserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Does_not_create_recursive_audit_log_entry_When_entity_is_saved()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid()) { Name = "Widget" };

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var logs = await context.AuditLogEntries.IgnoreQueryFilters().ToListAsync();
        logs.Should().ContainSingle();
    }

    [Fact]
    public async Task Creates_unique_non_empty_id_for_each_audit_log_entry_When_multiple_entities_saved()
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
}
