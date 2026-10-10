using System.Security.Claims;
using FluentAssertions;
using HrastERP.API.Authorization;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace HrastERP.API.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static async Task<AuthorizationHandlerContext> InvokeHandlerAsync(
        Permission userPermissions,
        Permission requiredPermission)
    {
        var user = Substitute.For<ICurrentUser>();
        user.EffectivePermissions.Returns(userPermissions);
        var handler = new PermissionAuthorizationHandler(user);
        var requirement = new PermissionRequirement(requiredPermission);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(),
            resource: null);

        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Succeeds_When_user_has_exact_required_permission()
    {
        var context = await InvokeHandlerAsync(Permission.FinanceView, Permission.FinanceView);
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Succeeds_When_user_has_required_permission_among_others()
    {
        var userPerms = Permission.FinanceView | Permission.InventoryCreate | Permission.ProcurementEdit;
        var context = await InvokeHandlerAsync(userPerms, Permission.InventoryCreate);
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Does_not_succeed_When_user_lacks_required_permission()
    {
        var context = await InvokeHandlerAsync(Permission.FinanceView, Permission.FinanceEdit);
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_succeed_When_user_has_no_permissions()
    {
        var context = await InvokeHandlerAsync(Permission.None, Permission.FinanceView);
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_succeed_When_user_has_all_permissions_except_required()
    {
        var allExceptFinanceEdit =
            Permission.FinanceView | Permission.FinanceCreate | Permission.FinanceDelete;
        var context = await InvokeHandlerAsync(allExceptFinanceEdit, Permission.FinanceEdit);
        context.HasSucceeded.Should().BeFalse();
    }
}
