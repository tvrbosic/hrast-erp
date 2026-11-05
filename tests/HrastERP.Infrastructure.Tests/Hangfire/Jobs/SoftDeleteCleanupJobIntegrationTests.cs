using System.Text.Json;
using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Database.Audit;
using HrastERP.Infrastructure.FileStorage;
using HrastERP.Infrastructure.Hangfire;
using HrastERP.Infrastructure.Hangfire.Jobs;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Hangfire.Jobs;

[Trait("Category", "Integration")]
public class SoftDeleteCleanupJobIntegrationTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly HrastDbContext _context;

    public SoftDeleteCleanupJobIntegrationTests(PostgresContainerFixture fixture)
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        _context = new HrastDbContext(
            new DbContextOptionsBuilder<HrastDbContext>().UseNpgsql(fixture.NewConnectionString()).Options,
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)],
            tenant);
    }

    public async Task InitializeAsync() => await _context.Database.MigrateAsync();

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Purges_tenant_in_administration_schema_When_soft_deleted_past_retention()
    {
        var id = await AddTenantDeletedAsync(TimeSpan.FromDays(91));

        await RunJobAsync();

        _context.Set<Tenant>().IgnoreQueryFilters().AsNoTracking().Any(t => t.Id == id).Should().BeFalse();
    }

    [Fact]
    public async Task Keeps_tenant_When_soft_deleted_within_retention()
    {
        var id = await AddTenantDeletedAsync(TimeSpan.FromDays(1));

        await RunJobAsync();

        _context.Set<Tenant>().IgnoreQueryFilters().AsNoTracking().Any(t => t.Id == id).Should().BeTrue();
    }

    [Fact]
    public async Task Writes_Purged_audit_entry_with_snake_case_keys_When_entity_is_purged()
    {
        var id = await AddTenantDeletedAsync(TimeSpan.FromDays(91));

        await RunJobAsync();

        var entry = _context.Set<AuditLogEntry>().IgnoreQueryFilters().AsNoTracking()
            .Single(e => e.EntityId == id.ToString() && e.Action == AuditAction.Purged);
        entry.EntityName.Should().Be("Tenant");
        var keys = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.OldValues!)!.Keys;
        keys.Should().Contain(["id", "name", "is_active", "deleted_at"]).And.NotContain("IsActive");
    }

    [Fact]
    public async Task Purges_all_rows_with_one_audit_entry_each_When_more_than_one_batch_is_eligible()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 101; i++)
            ids.Add(await AddTenantDeletedAsync(TimeSpan.FromDays(91)));

        await RunJobAsync();

        _context.Set<Tenant>().IgnoreQueryFilters().AsNoTracking().Any(t => ids.Contains(t.Id)).Should().BeFalse();
        var auditedIds = _context.Set<AuditLogEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Action == AuditAction.Purged && e.EntityName == "Tenant")
            .Select(e => e.EntityId)
            .ToList();
        auditedIds.Should().BeEquivalentTo(ids.Select(id => id.ToString()));
    }

    [Fact]
    public async Task Writes_entity_tenant_id_When_purged_entity_is_tenant_scoped()
    {
        var fileId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _context.Set<StoredFile>().Add(new StoredFile(fileId, "a.pdf", "application/pdf", 1, "x/a.pdf"));
        await _context.SaveChangesAsync();
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE stored_files SET tenant_id = {0}, deleted_at = {1} WHERE id = {2}",
            tenantId, DateTime.UtcNow - TimeSpan.FromDays(91), fileId);

        await RunJobAsync();

        var entry = _context.Set<AuditLogEntry>().IgnoreQueryFilters().AsNoTracking()
            .Single(e => e.EntityId == fileId.ToString() && e.Action == AuditAction.Purged);
        entry.EntityName.Should().Be("StoredFile");
        entry.TenantId.Should().Be(tenantId);
    }

    private async Task<Guid> AddTenantDeletedAsync(TimeSpan age)
    {
        var id = Guid.NewGuid();
        _context.Set<Tenant>().Add(new Tenant(id, $"Test tenant {id}"));
        await _context.SaveChangesAsync();

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE administration.tenants SET deleted_at = {0} WHERE id = {1}",
            DateTime.UtcNow - age, id);
        return id;
    }

    private Task RunJobAsync()
    {
        var job = new SoftDeleteCleanupJob(
            _context,
            Options.Create(new HangfireSettings { SoftDeleteRetentionDays = 90 }),
            NullLogger<SoftDeleteCleanupJob>.Instance);
        return job.ExecuteAsync(CancellationToken.None);
    }
}
