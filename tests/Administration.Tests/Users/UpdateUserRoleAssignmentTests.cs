using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Administration.Tests.Users;

[Trait("Category", "Integration")]
public class UpdateUserRoleAssignmentTests(PostgresContainerFixture fixture)
    : RoleAssignmentTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result> HandleAsync(
        Guid callerTenantId, Permission callerPermissions, ApplicationUser target, Guid? roleId, string firstName = "Test")
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(callerTenantId);
        await using var db = NewDbContext(callerTenantId);
        var sut = new UpdateUserCommandHandler(
            UserManagerThatSucceeds(), db, currentTenant, CurrentUserWith(callerPermissions));

        return await sut.Handle(
            new UpdateUserCommand(target.Id, target.Email!, firstName, target.LastName, roleId),
            CancellationToken.None);
    }

    private async Task<ApplicationUser> LoadAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await db.Set<ApplicationUser>().SingleAsync(u => u.Id == id);
    }

    [Fact]
    public async Task Returns_RoleExceedsOwnPermissions_When_tenant_admin_assigns_role_beyond_own_permissions()
    {
        var tenant = await SeedTenantAsync();
        var powerful = await SeedRoleAsync("Powerful", Permission.AdministrationView | Permission.FinanceDelete);
        var self = await SeedUserAsync(tenant.Id, roleId: null);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, self, powerful.Id);

        result.Error.Should().Be(AdministrationErrors.RoleExceedsOwnPermissions);
        (await LoadAsync(self.Id)).RoleId.Should().BeNull();
    }

    [Fact]
    public async Task Updates_role_When_tenant_admin_assigns_role_within_own_permissions()
    {
        var tenant = await SeedTenantAsync();
        var clerk = await SeedRoleAsync("FinanceClerk", Permission.FinanceView);
        var user = await SeedUserAsync(tenant.Id, roleId: null);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, user, clerk.Id);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(user.Id)).RoleId.Should().Be(clerk.Id);
    }

    [Fact]
    public async Task Returns_UserRoleExceedsOwnPermissions_When_target_users_current_role_exceeds_caller()
    {
        var tenant = await SeedTenantAsync();
        var manager = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);
        var clerk = await SeedRoleAsync("FinanceClerk", Permission.FinanceView);
        var colleague = await SeedUserAsync(tenant.Id, manager.Id);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, colleague, clerk.Id);

        result.Error.Should().Be(AdministrationErrors.UserRoleExceedsOwnPermissions);
        (await LoadAsync(colleague.Id)).RoleId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task Returns_UserRoleExceedsOwnPermissions_When_removing_role_that_exceeds_caller()
    {
        var tenant = await SeedTenantAsync();
        var manager = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);
        var colleague = await SeedUserAsync(tenant.Id, manager.Id);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, colleague, roleId: null);

        result.Error.Should().Be(AdministrationErrors.UserRoleExceedsOwnPermissions);
    }

    [Fact]
    public async Task Updates_other_fields_When_target_role_exceeds_caller_but_role_is_unchanged()
    {
        var tenant = await SeedTenantAsync();
        var manager = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);
        var colleague = await SeedUserAsync(tenant.Id, manager.Id);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, colleague, manager.Id, firstName: "Renamed");

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(colleague.Id)).FirstName.Should().Be("Renamed");
    }

    [Fact]
    public async Task Updates_role_When_super_admin_assigns_role_beyond_own_permissions()
    {
        var tenant = await SeedTenantAsync();
        var manager = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);
        var user = await SeedUserAsync(tenant.Id, roleId: null);

        var result = await HandleAsync(TenantConstants.SuperAdminTenantId, Permission.AdministrationEdit, user, manager.Id);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(user.Id)).RoleId.Should().Be(manager.Id);
    }
}
