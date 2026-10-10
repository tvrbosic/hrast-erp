using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class TenantEntityInterceptorTests : IClassFixture<PostgresContainerFixture>
{
    private sealed class TestTenantEntity(Guid id) : BaseEntity<Guid>(id), ITenantEntity
    {
        public Guid TenantId { get; set; }
    }

    private sealed class TestNonTenantEntity(Guid id) : BaseEntity<Guid>(id);

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

    private readonly PostgresContainerFixture _fixture;

    public TenantEntityInterceptorTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TestDbContext CreateContext(Guid tenantId)
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(tenantId);
        var interceptor = new TenantEntityInterceptor(currentTenant);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.NewConnectionString())
            .AddInterceptors(interceptor)
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Sets_TenantId_When_entity_is_added()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var entity = new TestTenantEntity(Guid.NewGuid());

        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Throws_InvalidOperationException_When_TenantId_is_empty_on_insert()
    {
        await using var context = CreateContext(Guid.Empty);
        var entity = new TestTenantEntity(Guid.NewGuid());

        context.TenantEntities.Add(entity);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task Does_not_change_TenantId_When_entity_is_modified()
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
    public async Task Does_not_throw_When_entity_does_not_implement_ITenantEntity()
    {
        await using var context = CreateContext(Guid.Empty);
        var entity = new TestNonTenantEntity(Guid.NewGuid());

        context.NonTenantEntities.Add(entity);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Does_not_overwrite_TenantId_When_already_set()
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
    public async Task Sets_current_tenant_id_When_TenantId_is_empty()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var entity = new TestTenantEntity(Guid.NewGuid()) { TenantId = Guid.Empty };

        context.TenantEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.TenantId.Should().Be(tenantId);
    }
}
