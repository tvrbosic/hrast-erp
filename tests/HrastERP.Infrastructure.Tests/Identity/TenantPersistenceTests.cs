using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Identity;

[Trait("Category", "Integration")]
public class TenantPersistenceTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly HrastDbContext _context;

    public TenantPersistenceTests(PostgresContainerFixture fixture)
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        _context = new HrastDbContext(
            new DbContextOptionsBuilder<HrastDbContext>().UseNpgsql(fixture.NewConnectionString()).Options,
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)],
            tenant);
    }

    public async Task InitializeAsync() => await _context.Database.MigrateAsync();

    public async Task DisposeAsync() => await _context.DisposeAsync();

    // Seed scripts insert tenants with raw SQL; one that forgets normalized_name must fail loudly instead of
    // storing '' that the handlers' duplicate check would never match.
    [Fact]
    public async Task Throws_not_null_violation_When_raw_insert_omits_normalized_name()
    {
        var act = () => _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO administration.tenants (id, name, is_active, created_at, created_by) " +
            "VALUES (gen_random_uuid(), 'Acme', true, now(), '00000000-0000-0000-0000-000000000000')");

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.NotNullViolation);
    }
}
