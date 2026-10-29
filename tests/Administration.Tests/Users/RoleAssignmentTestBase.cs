using Administration.Tests.Fixtures;
using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Administration.Tests.Users;

public abstract class RoleAssignmentTestBase(PostgresContainerFixture fixture) : IntegrationTestBase(fixture)
{
    // Tenant admin who may manage users and holds only Finance view/edit beyond that.
    protected const Permission TenantAdminPermissions =
        Permission.AdministrationView | Permission.AdministrationCreate | Permission.AdministrationEdit
        | Permission.FinanceView | Permission.FinanceEdit;

    protected static ICurrentUser CurrentUserWith(Permission permissions)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.EffectivePermissions.Returns(permissions);
        return currentUser;
    }

    protected static UserManager<ApplicationUser> UserManagerThatSucceeds()
    {
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        return userManager;
    }
}
