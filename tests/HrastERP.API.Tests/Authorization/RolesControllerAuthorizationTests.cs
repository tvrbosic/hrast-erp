using System.Reflection;
using FluentAssertions;
using HrastERP.API.Authorization;
using HrastERP.API.Controllers;
using HrastERP.SharedKernel.Authorization;

namespace HrastERP.API.Tests.Authorization;

public class RolesControllerAuthorizationTests
{
    private static MethodInfo Action(string name) =>
        typeof(RolesController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;

    [Theory]
    [InlineData(nameof(RolesController.Create))]
    [InlineData(nameof(RolesController.Update))]
    [InlineData(nameof(RolesController.Delete))]
    public void Requires_super_admin_tenant_When_action_writes(string action)
    {
        Action(action).GetCustomAttribute<RequireSuperAdminTenantAttribute>().Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(RolesController.GetAll))]
    [InlineData(nameof(RolesController.GetById))]
    public void Does_not_require_super_admin_tenant_When_action_reads(string action)
    {
        Action(action).GetCustomAttribute<RequireSuperAdminTenantAttribute>().Should().BeNull();
    }

    [Fact]
    public void Does_not_require_super_admin_tenant_When_checking_controller_level()
    {
        typeof(RolesController).GetCustomAttribute<RequireSuperAdminTenantAttribute>().Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(RolesController.GetAll), Permission.AdministrationView)]
    [InlineData(nameof(RolesController.GetById), Permission.AdministrationView)]
    [InlineData(nameof(RolesController.Create), Permission.AdministrationCreate)]
    [InlineData(nameof(RolesController.Update), Permission.AdministrationEdit)]
    [InlineData(nameof(RolesController.Delete), Permission.AdministrationDelete)]
    public void Requires_expected_permission_When_action_is_invoked(string action, Permission expected)
    {
        Action(action).GetCustomAttributes<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .Should().Equal(expected);
    }
}
