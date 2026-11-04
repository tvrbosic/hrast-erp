using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Roles;
using HrastERP.Administration.Application.Roles.Commands.DeleteRole;
using HrastERP.Infrastructure.Authorization;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Administration.Tests.Roles;

[Trait("Category", "Integration")]
public class DeleteRoleCommandHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result> HandleAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await new DeleteRoleCommandHandler(db).Handle(new DeleteRoleCommand(id), CancellationToken.None);
    }

    [Fact]
    public async Task Deletes_role_When_no_users_are_assigned()
    {
        var role = await SeedRoleAsync("Temp");

        var result = await HandleAsync(role.Id);

        result.IsSuccess.Should().BeTrue();
        await using var db = NewDbContext();
        (await db.Set<Role>().AnyAsync(r => r.Id == role.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Returns_NotFound_When_role_does_not_exist()
    {
        var result = await HandleAsync(Guid.NewGuid());

        result.Error.Should().Be(AdministrationErrors.RoleNotFound);
    }

    [Fact]
    public async Task Returns_AdministratorImmutable_When_deleting_Administrator()
    {
        await SeedRoleAsync("Administrator", PermissionMapper.All, RoleConstants.AdministratorRoleId);

        var result = await HandleAsync(RoleConstants.AdministratorRoleId);

        result.Error.Should().Be(AdministrationErrors.RoleAdministratorImmutable);
    }

    [Fact]
    public async Task Returns_InUse_When_role_is_assigned_to_user_in_another_tenant()
    {
        var role = await SeedRoleAsync("Clerk");
        var tenant = await SeedTenantAsync();
        await SeedUserAsync(tenant.Id, role.Id);
        await SeedUserAsync(tenant.Id, role.Id);

        var result = await HandleAsync(role.Id);

        result.Error.Should().Be(AdministrationErrors.RoleInUse(2));
    }

    [Fact]
    public async Task Returns_InUse_When_role_is_assigned_to_inactive_user()
    {
        var role = await SeedRoleAsync("Clerk");
        var tenant = await SeedTenantAsync();
        await SeedUserAsync(tenant.Id, role.Id, isActive: false);

        var result = await HandleAsync(role.Id);

        result.Error.Should().Be(AdministrationErrors.RoleInUse(1));
    }

    [Fact]
    public async Task Throws_DbUpdateException_When_role_is_assigned_concurrently_after_check()
    {
        var role = await SeedRoleAsync("Clerk");
        var tenant = await SeedTenantAsync();
        var race = new BeforeSaveInterceptor(() => SeedUserAsync(tenant.Id, role.Id));
        await using var db = NewDbContext(interceptors: race);

        var act = () => new DeleteRoleCommandHandler(db)
            .Handle(new DeleteRoleCommand(role.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verify = NewDbContext();
        (await verify.Set<Role>().AnyAsync(r => r.Id == role.Id)).Should().BeTrue();
    }
}
