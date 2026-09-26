using FluentAssertions;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using HrastERP.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Database;

public class TenantEntityInterceptorTests
{
    private sealed class TestTenantEntity(Guid id) : BaseEntity<Guid>(id), ITenantEntity
    {
        public Guid TenantId { get; set; }
    }

    private sealed class TestNonTenantEntity(Guid id) : BaseEntity<Guid>(id);

    private sealed class FakeCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestTenantEntity> TenantEntities => Set<TestTenantEntity>();
        public DbSet<TestNonTenantEntity> NonTenantEntities => Set<TestNonTenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestTenantEntity>(b => b.HasKey(e => e.Id));
            modelBuilder.Entity<TestNonTenantEntity>(b => b.HasKey(e => e.Id));
        }
    }

    private static TestDbContext CreateContext(Guid tenantId)
    {
        var currentTenant = new FakeCurrentTenant(tenantId);
        var interceptor = new TenantEntityInterceptor(currentTenant);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    [Fact]
    public async Task Added_entity_gets_TenantId_set()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var entity = new TestTenantEntity(Guid.NewGuid());

        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Throws_when_TenantId_is_empty_on_add()
    {
        await using var context = CreateContext(Guid.Empty);
        var entity = new TestTenantEntity(Guid.NewGuid());

        context.TenantEntities.Add(entity);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task Modified_entity_TenantId_is_not_changed()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var entity = new TestTenantEntity(Guid.NewGuid());

        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync();

        entity.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Non_tenant_entity_is_ignored()
    {
        await using var context = CreateContext(Guid.Empty);
        var entity = new TestNonTenantEntity(Guid.NewGuid());

        context.NonTenantEntities.Add(entity);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Added_entity_with_TenantId_already_set_is_not_overwritten()
    {
        var currentTenantId = Guid.NewGuid();
        var presetTenantId = Guid.NewGuid();
        await using var context = CreateContext(currentTenantId);
        var entity = new TestTenantEntity(Guid.NewGuid()) { TenantId = presetTenantId };

        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.TenantId.Should().Be(presetTenantId);
    }

    [Fact]
    public async Task Added_entity_with_empty_TenantId_gets_current_tenant()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var entity = new TestTenantEntity(Guid.NewGuid()) { TenantId = Guid.Empty };

        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.TenantId.Should().Be(tenantId);
    }
}
