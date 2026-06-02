using FluentAssertions;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using HrastERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Infrastructure.Tests.Persistence;

public class AuditableEntityInterceptorTests : IDisposable
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
            modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasKey(e => e.Id);
            });
        }
    }

    private readonly Guid _userId = Guid.NewGuid();

    private TestDbContext CreateContext(bool isAuthenticated = true)
    {
        var currentUser = new FakeCurrentUser(_userId, isAuthenticated);
        var interceptor = new AuditableEntityInterceptor(currentUser);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    [Fact]
    public async Task Added_entity_gets_CreatedAt_and_CreatedBy_set()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.CreatedBy.Should().Be(_userId);
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public async Task Modified_entity_gets_UpdatedAt_and_UpdatedBy_set()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        var originalCreatedAt = entity.CreatedAt;
        var originalCreatedBy = entity.CreatedBy;

        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync();

        entity.CreatedAt.Should().Be(originalCreatedAt);
        entity.CreatedBy.Should().Be(originalCreatedBy);
        entity.UpdatedAt.Should().NotBeNull();
        entity.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.UpdatedBy.Should().Be(_userId);
    }

    [Fact]
    public async Task Unauthenticated_user_sets_empty_guid()
    {
        await using var context = CreateContext(isAuthenticated: false);
        var entity = new TestEntity(Guid.NewGuid());

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.CreatedBy.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task CreatedAt_is_utc()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    public void Dispose()
    {
        // InMemory databases are disposed with the context
    }
}
