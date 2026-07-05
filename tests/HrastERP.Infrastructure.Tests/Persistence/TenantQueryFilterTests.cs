using System.Linq.Expressions;
using FluentAssertions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Persistence;

public class TenantQueryFilterTests
{
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
                var tenantEquals = Expression.Equal(tenantIdProp, currentTenantIdProp);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(tenantEquals, parameter));
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
}
