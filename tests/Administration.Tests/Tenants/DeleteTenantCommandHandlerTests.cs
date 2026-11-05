using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Tenants.Commands.DeleteTenant;
using HrastERP.Administration.Domain.Entities;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Administration.Tests.Tenants;

[Trait("Category", "Integration")]
public class DeleteTenantCommandHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result> HandleAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await new DeleteTenantCommandHandler(db).Handle(new DeleteTenantCommand(id), CancellationToken.None);
    }

    [Fact]
    public async Task Deletes_tenant_When_it_has_no_users()
    {
        var tenant = await SeedTenantAsync();

        var result = await HandleAsync(tenant.Id);

        result.IsSuccess.Should().BeTrue();
        await using var db = NewDbContext();
        (await db.Set<Tenant>().AnyAsync(t => t.Id == tenant.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Returns_NotFound_When_tenant_does_not_exist()
    {
        var result = await HandleAsync(Guid.NewGuid());

        result.Error.Should().Be(AdministrationErrors.TenantNotFound);
    }

    [Fact]
    public async Task Returns_HasUsers_When_tenant_has_active_and_inactive_users()
    {
        var tenant = await SeedTenantAsync();
        await SeedUserAsync(tenant.Id, roleId: null);
        await SeedUserAsync(tenant.Id, roleId: null, isActive: false);

        var result = await HandleAsync(tenant.Id);

        result.Error.Should().Be(AdministrationErrors.TenantHasUsers(2));
        await using var db = NewDbContext();
        (await db.Set<Tenant>().AnyAsync(t => t.Id == tenant.Id)).Should().BeTrue();
    }
}
