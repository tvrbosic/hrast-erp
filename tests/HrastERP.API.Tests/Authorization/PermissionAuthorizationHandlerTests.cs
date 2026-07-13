using System.Security.Claims;
using FluentAssertions;
using HrastERP.API.Authorization;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private sealed class FakeCurrentUser(Permission permissions) : ICurrentUser
    {
        public Guid UserId => Guid.NewGuid();
        public Guid TenantId => Guid.NewGuid();
        public string Username => "test@example.com";
        public bool IsAuthenticated => true;
        public Permission EffectivePermissions => permissions;
    }

    private static async Task<AuthorizationHandlerContext> InvokeHandlerAsync(
        Permission userPermissions,
        Permission requiredPermission)
    {
        var handler = new PermissionAuthorizationHandler(new FakeCurrentUser(userPermissions));
        var requirement = new PermissionRequirement(requiredPermission);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(),
            resource: null);

        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task UserHasExactPermission_Succeeds()
    {
        var context = await InvokeHandlerAsync(Permission.FinanceView, Permission.FinanceView);
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasPermissionAmongOthers_Succeeds()
    {
        var userPerms = Permission.FinanceView | Permission.InventoryCreate | Permission.ProcurementEdit;
        var context = await InvokeHandlerAsync(userPerms, Permission.InventoryCreate);
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task UserMissingPermission_DoesNotSucceed()
    {
        var context = await InvokeHandlerAsync(Permission.FinanceView, Permission.FinanceEdit);
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasNoPermissions_DoesNotSucceed()
    {
        var context = await InvokeHandlerAsync(Permission.None, Permission.FinanceView);
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasAllPermissionsExceptRequired_DoesNotSucceed()
    {
        var allExceptFinanceEdit =
            Permission.FinanceView | Permission.FinanceCreate | Permission.FinanceDelete;
        var context = await InvokeHandlerAsync(allExceptFinanceEdit, Permission.FinanceEdit);
        context.HasSucceeded.Should().BeFalse();
    }
}
