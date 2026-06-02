using System.Linq.Expressions;
using FluentAssertions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Persistence;

public class SoftDeleteQueryFilterTests
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b => b.HasKey(e => e.Id));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                    continue;

                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var deletedAtProperty = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
                var nullConstant = Expression.Constant(null, typeof(DateTime?));
                var isNull = Expression.Equal(deletedAtProperty, nullConstant);
                var lambda = Expression.Lambda(isNull, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options);
    }

    [Fact]
    public async Task Non_deleted_entities_are_returned_by_default()
    {
        await using var context = CreateContext();
        context.TestEntities.Add(new TestEntity(Guid.NewGuid()));
        await context.SaveChangesAsync();

        var count = await context.TestEntities.CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Entities_with_DeletedAt_set_are_hidden_from_queries()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.Entry(entity).Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var count = await context.TestEntities.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task IgnoreQueryFilters_returns_soft_deleted_entities()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.Entry(entity).Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var count = await context.TestEntities.IgnoreQueryFilters().CountAsync();
        count.Should().Be(1);
    }
}
