using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Identity;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class DatabaseSeederTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly HrastDbContext _context;

    public DatabaseSeederTests(PostgresContainerFixture fixture)
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

    private DatabaseSeeder CreateSeeder(bool seedFixtures) =>
        new(_context, Options.Create(new DatabaseSettings { ConnectionString = "unused", SeedFixtures = seedFixtures }));

    [Fact]
    public async Task Applies_reference_and_fixture_scripts_When_SeedFixtures_is_true()
    {
        await CreateSeeder(seedFixtures: true).SeedAsync();

        _context.Set<Role>().IgnoreQueryFilters().Count().Should().Be(5);
        _context.Set<Tenant>().IgnoreQueryFilters().Should().Contain(t => t.Id == TenantConstants.SuperAdminTenantId);
        _context.Users.IgnoreQueryFilters().Should()
            .Contain(u => u.UserName == "admin" && u.RoleId == RoleConstants.AdministratorRoleId);
    }

    [Fact]
    public async Task Applies_only_reference_scripts_When_SeedFixtures_is_false()
    {
        await CreateSeeder(seedFixtures: false).SeedAsync();

        _context.Set<Role>().IgnoreQueryFilters().Count().Should().Be(5);
        _context.Set<Tenant>().IgnoreQueryFilters().Should().Contain(t => t.Id == TenantConstants.SuperAdminTenantId);
        _context.Users.IgnoreQueryFilters().Should().NotContain(u => u.UserName == "admin");
    }
}
