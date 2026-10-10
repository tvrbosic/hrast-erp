using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class AuditableEntityInterceptorTests : IClassFixture<PostgresContainerFixture>
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id);

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

    private readonly PostgresContainerFixture _fixture;
    private readonly Guid _userId = Guid.NewGuid();

    public AuditableEntityInterceptorTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TestDbContext CreateContext(bool isAuthenticated = true)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(isAuthenticated);
        currentUser.UserId.Returns(_userId);
        var interceptor = new AuditableEntityInterceptor(currentUser);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.NewConnectionString())
            .AddInterceptors(interceptor)
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Sets_CreatedAt_and_CreatedBy_When_entity_is_added()
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
    public async Task Sets_UpdatedAt_and_UpdatedBy_When_entity_is_modified()
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
    public async Task Sets_empty_guid_When_user_is_unauthenticated()
    {
        await using var context = CreateContext(isAuthenticated: false);
        var entity = new TestEntity(Guid.NewGuid());

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.CreatedBy.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task CreatedAt_has_UTC_kind_When_entity_is_added()
    {
        await using var context = CreateContext();
        var entity = new TestEntity(Guid.NewGuid());

        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
