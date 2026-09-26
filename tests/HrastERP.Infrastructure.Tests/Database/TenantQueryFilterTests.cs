using System.Linq.Expressions;
using FluentAssertions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Database;

public class TenantQueryFilterTests
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

    private static TestDbContext CreateContext(Guid currentTenantId)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options, currentTenantId);
    }

    [Fact]
    public async Task Entities_matching_current_tenant_are_returned()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);

        context.TenantEntities.Add(new TenantTestEntity(Guid.NewGuid()) { TenantId = tenantId });
        await context.SaveChangesAsync();

        var count = await context.TenantEntities.CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Entities_with_different_TenantId_are_hidden()
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
    public async Task Non_tenant_entities_are_not_filtered()
    {
        await using var context = CreateContext(Guid.NewGuid());

        context.NonTenantEntities.Add(new NonTenantTestEntity(Guid.NewGuid()));
        await context.SaveChangesAsync();

        var count = await context.NonTenantEntities.CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task IgnoreQueryFilters_returns_all_tenants()
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
    public async Task Super_admin_tenant_sees_all_entities()
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
    public async Task Regular_tenant_does_not_see_other_tenants()
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
