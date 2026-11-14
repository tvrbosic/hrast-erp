using System.Security.Claims;
using FluentAssertions;
using HrastERP.API.Authorization;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace HrastERP.API.Tests.Authorization;

public class SuperAdminTenantAuthorizationHandlerTests
{
    private static async Task<AuthorizationHandlerContext> InvokeHandlerAsync(Guid tenantId)
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(tenantId);
        var handler = new SuperAdminTenantAuthorizationHandler(tenant);
        var context = new AuthorizationHandlerContext(
            [new SuperAdminTenantRequirement()],
            new ClaimsPrincipal(),
            resource: null);

        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Succeeds_When_user_is_in_super_admin_tenant()
    {
        var context = await InvokeHandlerAsync(TenantConstants.SuperAdminTenantId);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_with_SuperAdminRequired_reason_When_user_is_in_other_tenant()
    {
        var context = await InvokeHandlerAsync(Guid.NewGuid());

        context.HasFailed.Should().BeTrue();
        context.FailureReasons.OfType<ErrorFailureReason>()
            .Should().ContainSingle()
            .Which.Error.Should().Be(ApiErrors.SuperAdminTenantRequired);
    }
}
