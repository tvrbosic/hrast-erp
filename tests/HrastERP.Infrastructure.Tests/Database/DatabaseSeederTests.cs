using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Database;

[Trait("Category", "Integration")]
public class DatabaseSeederTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly HrastDbContext _context;
    private readonly IWebHostEnvironment _env;

    public DatabaseSeederTests(PostgresContainerFixture fixture)
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        _context = new HrastDbContext(
            new DbContextOptionsBuilder<HrastDbContext>().UseNpgsql(fixture.NewConnectionString()).Options,
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)],
            tenant);

        _env = Substitute.For<IWebHostEnvironment>();
        _env.EnvironmentName = "Development";
    }

    public async Task InitializeAsync() => await _context.Database.MigrateAsync();

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Applies_all_seed_scripts_When_database_is_freshly_migrated()
    {
        await new DatabaseSeeder(_context, _env).SeedAsync();

        _context.Set<Role>().IgnoreQueryFilters().Count().Should().Be(5);
        _context.Set<Tenant>().IgnoreQueryFilters().Should().Contain(t => t.Id == TenantConstants.SuperAdminTenantId);
        _context.Users.IgnoreQueryFilters().Should()
            .Contain(u => u.UserName == "admin" && u.RoleId == RoleConstants.AdministratorRoleId);
    }
}
