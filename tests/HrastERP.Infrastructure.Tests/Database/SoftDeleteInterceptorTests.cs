using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class SoftDeleteInterceptorTests : IClassFixture<PostgresContainerFixture>
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b => b.HasKey(e => e.Id));
        }
    }

    private readonly PostgresContainerFixture _fixture;
    private readonly Guid _userId = Guid.NewGuid();

    public SoftDeleteInterceptorTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TestDbContext CreateContext(bool isAuthenticated = true)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(isAuthenticated);
        currentUser.UserId.Returns(_userId);
        var interceptor = new SoftDeleteInterceptor(currentUser);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.NewConnectionString())
            .AddInterceptors(interceptor)
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Does_not_delete_entity_from_database_When_entity_is_removed()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        var count = await context.TestEntities.IgnoreQueryFilters().CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Sets_DeletedAt_When_entity_is_removed()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        entity.DeletedAt.Should().NotBeNull();
        entity.DeletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Sets_DeletedBy_to_current_user_When_entity_is_removed()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        entity.DeletedBy.Should().Be(_userId);
    }

    [Fact]
    public async Task Sets_empty_guid_for_DeletedBy_When_user_is_unauthenticated()
    {
        await using var context = CreateContext(isAuthenticated: false);
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        entity.DeletedBy.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task DeletedAt_has_UTC_kind_When_entity_is_removed()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        entity.DeletedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
