using System.Linq.Expressions;
using FluentAssertions;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class SoftDeleteQueryFilterTests : IClassFixture<PostgresContainerFixture>
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

    private readonly PostgresContainerFixture _fixture;

    public SoftDeleteQueryFilterTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.NewConnectionString())
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Returns_non_deleted_entities_When_no_filter_override()
    {
        await using var context = CreateContext();
        context.TestEntities.Add(new TestEntity(Guid.NewGuid()));
        await context.SaveChangesAsync();

        var count = await context.TestEntities.CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Hides_entity_from_queries_When_DeletedAt_is_set()
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
    public async Task Returns_soft_deleted_entities_When_IgnoreQueryFilters_is_used()
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
