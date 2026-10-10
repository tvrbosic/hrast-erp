using System.Linq.Expressions;
using FluentAssertions;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class TenantQueryFilterTests : IClassFixture<PostgresContainerFixture>
{
    private static readonly Guid SuperAdminTenantId = Guid.Parse("00000000-0000-0000-0000-100000000000");

    private sealed class TenantTestEntity(Guid id) : BaseEntity<Guid>(id), ITenantEntity
    {
        public Guid TenantId { get; set; }
    }

    private sealed class NonTenantTestEntity(Guid id) : BaseEntity<Guid>(id);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, Guid currentTenantId) : DbContext(options)
    {
        private Guid CurrentTenantId { get; } = currentTenantId;

        public DbSet<TenantTestEntity> TenantEntities => Set<TenantTestEntity>();
        public DbSet<NonTenantTestEntity> NonTenantEntities => Set<NonTenantTestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantTestEntity>(b => b.HasKey(e => e.Id));
            modelBuilder.Entity<NonTenantTestEntity>(b => b.HasKey(e => e.Id));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                    continue;

                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var dbContextRef = Expression.Constant(this, typeof(TestDbContext));
                var currentTenantIdProp = Expression.Property(dbContextRef, nameof(CurrentTenantId));

                var superAdminConst = Expression.Constant(SuperAdminTenantId, typeof(Guid));
                var isSuperAdmin = Expression.Equal(currentTenantIdProp, superAdminConst);
                var tenantEquals = Expression.Equal(tenantIdProp, currentTenantIdProp);
                var tenantFilter = Expression.OrElse(isSuperAdmin, tenantEquals);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(tenantFilter, parameter));
            }
        }
    }

    private readonly PostgresContainerFixture _fixture;

    public TenantQueryFilterTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TestDbContext CreateContext(Guid currentTenantId)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.NewConnectionString())
            .Options;

        var context = new TestDbContext(options, currentTenantId);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Returns_matching_entities_When_tenant_id_matches()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);

        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = tenantId });
        await context.SaveChangesAsync();

        var count = await context.TenantEntities.CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Hides_entity_When_tenant_id_does_not_match()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);

        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = otherTenantId });
        await context.SaveChangesAsync();

        var count = await context.TenantEntities.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Returns_all_entities_When_entity_does_not_implement_ITenantEntity()
    {
        await using var context = CreateContext(Guid.NewGuid());

        context.NonTenantEntities.Add(new NonTenantTestEntity(Guid.NewGuid()));
        await context.SaveChangesAsync();

        var count = await context.NonTenantEntities.CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Returns_all_tenant_entities_When_IgnoreQueryFilters_is_used()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);

        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = tenantId });
        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = otherTenantId });
        await context.SaveChangesAsync();

        var count = await context.TenantEntities.IgnoreQueryFilters().CountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task Returns_all_entities_When_current_tenant_is_super_admin()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var context = CreateContext(SuperAdminTenantId);

        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = tenantA });
        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = tenantB });
        await context.SaveChangesAsync();

        var count = await context.TenantEntities.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Hides_other_tenants_entities_When_tenant_is_regular()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var context = CreateContext(tenantA);

        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = tenantA });
        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = tenantB });
        await context.SaveChangesAsync();

        var count = await context.TenantEntities.CountAsync();
        count.Should().Be(1);
    }
}
