using FluentAssertions;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using HrastERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Persistence;

public class SoftDeleteInterceptorTests
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id);

    private sealed class FakeCurrentUser(Guid userId, bool isAuthenticated) : ICurrentUser
    {
        public Guid UserId => userId;
        public Guid TenantId => Guid.NewGuid();
        public string Username => "testuser";
        public bool IsAuthenticated => isAuthenticated;
        public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b => b.HasKey(e => e.Id));
        }
    }

    private readonly Guid _userId = Guid.NewGuid();

    private TestDbContext CreateContext(bool isAuthenticated = true)
    {
        var currentUser = new FakeCurrentUser(_userId, isAuthenticated);
        var interceptor = new SoftDeleteInterceptor(currentUser);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    [Fact]
    public async Task Removed_entity_is_not_deleted_from_database()
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
    public async Task Removed_entity_gets_DeletedAt_set()
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
    public async Task Removed_entity_gets_DeletedBy_set_to_current_user()
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
    public async Task Unauthenticated_user_sets_empty_guid_for_DeletedBy()
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
    public async Task DeletedAt_is_utc()
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
