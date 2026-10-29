using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Users.Commands.CreateUser;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using NSubstitute;

namespace Administration.Tests.Users;

[Trait("Category", "Integration")]
public class CreateUserRoleAssignmentTests(PostgresContainerFixture fixture)
    : RoleAssignmentTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result<Guid>> HandleAsync(Guid callerTenantId, Permission callerPermissions, Guid targetTenantId, Guid roleId)
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(callerTenantId);
        await using var db = NewDbContext(callerTenantId);
        var sut = new CreateUserCommandHandler(
            UserManagerThatSucceeds(), db, currentTenant, CurrentUserWith(callerPermissions));

        return await sut.Handle(
            new CreateUserCommand("new@example.com", "Passw0rd!", "New", "User", targetTenantId, roleId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Returns_RoleExceedsOwnPermissions_When_tenant_admin_assigns_role_beyond_own_permissions()
    {
        var tenant = await SeedTenantAsync();
        var role = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, tenant.Id, role.Id);

        result.Error.Should().Be(AdministrationErrors.RoleExceedsOwnPermissions);
    }

    [Fact]
    public async Task Creates_user_When_tenant_admin_assigns_role_within_own_permissions()
    {
        var tenant = await SeedTenantAsync();
        var role = await SeedRoleAsync("FinanceClerk", Permission.FinanceView | Permission.FinanceEdit);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, tenant.Id, role.Id);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Creates_user_When_super_admin_assigns_role_beyond_own_permissions()
    {
        var tenant = await SeedTenantAsync();
        var role = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);

        var result = await HandleAsync(TenantConstants.SuperAdminTenantId, Permission.AdministrationCreate, tenant.Id, role.Id);

        result.IsSuccess.Should().BeTrue();
    }
}
