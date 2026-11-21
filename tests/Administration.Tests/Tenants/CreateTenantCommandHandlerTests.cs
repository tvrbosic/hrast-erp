using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Tenants.Commands.CreateTenant;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Administration.Tests.Tenants;

[Trait("Category", "Integration")]
public class CreateTenantCommandHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result<Guid>> HandleAsync(string name)
    {
        await using var db = NewDbContext();
        return await new CreateTenantCommandHandler(db).Handle(new CreateTenantCommand(name), CancellationToken.None);
    }

    [Fact]
    public async Task Creates_tenant_with_trimmed_name_When_name_has_surrounding_whitespace()
    {
        var result = await HandleAsync("  Acme ");

        result.IsSuccess.Should().BeTrue();
        await using var db = NewDbContext();
        var tenant = await db.Set<Tenant>().SingleAsync(t => t.Id == result.Value);
        tenant.Name.Should().Be("Acme");
    }

    [Fact]
    public async Task Returns_NameTaken_When_name_differs_only_in_case_or_whitespace()
    {
        await SeedTenantAsync("Acme");

        var result = await HandleAsync(" acme ");

        result.Error.Should().Be(AdministrationErrors.TenantNameTaken);
    }

    [Fact]
    public async Task Returns_NameTaken_When_name_differs_only_in_unicode_case()
    {
        await SeedTenantAsync("Čelik");

        var result = await HandleAsync("čelik");

        result.Error.Should().Be(AdministrationErrors.TenantNameTaken);
    }

    [Fact]
    public async Task Returns_NameTaken_When_name_belongs_to_soft_deleted_tenant()
    {
        var deleted = await SeedTenantAsync("Acme");
        await SoftDeleteTenantAsync(deleted.Id);

        var result = await HandleAsync("Acme");

        result.Error.Should().Be(AdministrationErrors.TenantNameTaken);
    }
}
