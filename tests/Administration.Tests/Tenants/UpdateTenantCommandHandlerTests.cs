using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Administration.Tests.Tenants;

[Trait("Category", "Integration")]
public class UpdateTenantCommandHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result> HandleAsync(Guid id, string name)
    {
        await using var db = NewDbContext();
        return await new UpdateTenantCommandHandler(db).Handle(new UpdateTenantCommand(id, name), CancellationToken.None);
    }

    [Fact]
    public async Task Returns_NameTaken_When_another_tenant_has_name_in_different_case()
    {
        await SeedTenantAsync("Acme");
        var tenant = await SeedTenantAsync("Globex");

        var result = await HandleAsync(tenant.Id, "ACME");

        result.Error.Should().Be(AdministrationErrors.TenantNameTaken);
    }

    [Fact]
    public async Task Returns_NameTaken_When_name_belongs_to_soft_deleted_tenant()
    {
        var deleted = await SeedTenantAsync("Acme");
        await SoftDeleteTenantAsync(deleted.Id);
        var tenant = await SeedTenantAsync("Globex");

        var result = await HandleAsync(tenant.Id, "Acme");

        result.Error.Should().Be(AdministrationErrors.TenantNameTaken);
    }

    [Fact]
    public async Task Renames_tenant_When_new_name_is_own_name_in_different_case()
    {
        var tenant = await SeedTenantAsync("Acme");

        var result = await HandleAsync(tenant.Id, " ACME ");

        result.IsSuccess.Should().BeTrue();
        await using var db = NewDbContext();
        (await db.Set<Tenant>().SingleAsync(t => t.Id == tenant.Id)).Name.Should().Be("ACME");
    }

    [Fact]
    public async Task Returns_NotFound_When_tenant_does_not_exist()
    {
        var result = await HandleAsync(Guid.NewGuid(), "Acme");

        result.Error.Should().Be(AdministrationErrors.TenantNotFound);
    }
}
