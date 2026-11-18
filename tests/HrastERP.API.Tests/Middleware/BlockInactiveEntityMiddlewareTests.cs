using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using HrastERP.API.Middleware;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HrastERP.API.Tests.Middleware;

// Statuses are served from the cache substitute, so no database is needed.
public class BlockInactiveEntityMiddlewareTests
{
    private static readonly Guid RegularTenantId = Guid.NewGuid();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private bool _nextCalled;

    private BlockInactiveEntityMiddleware CreateSut() => new(
        _ => { _nextCalled = true; return Task.CompletedTask; },
        _cache,
        Substitute.For<IServiceScopeFactory>(),
        NullLogger<BlockInactiveEntityMiddleware>.Instance);

    private static DefaultHttpContext CreateContext(Guid tenantId, Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(JwtClaimNames.TenantId, tenantId.ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test");
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            Response = { Body = new MemoryStream() }
        };
    }

    private static string? ResponseCode(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonSerializer.Deserialize<JsonElement>(context.Response.Body).GetProperty("code").GetString();
    }

    private void SetTenantActive(Guid tenantId, bool active) =>
        _cache.GetAsync<bool?>($"Tenants:active:{tenantId}").Returns(active);

    private void SetUserActive(Guid userId, bool active) =>
        _cache.GetAsync<bool?>($"Users:active:{userId}").Returns(active);

    [Fact]
    public async Task Returns_403_InactiveUser_When_super_admin_tenant_user_is_inactive()
    {
        var userId = Guid.NewGuid();
        SetUserActive(userId, false);
        var context = CreateContext(TenantConstants.SuperAdminTenantId, userId);

        await CreateSut().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        ResponseCode(context).Should().Be(AuthErrors.InactiveUser.Code);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Calls_next_without_tenant_check_When_super_admin_tenant_user_is_active()
    {
        var userId = Guid.NewGuid();
        SetUserActive(userId, true);
        SetTenantActive(TenantConstants.SuperAdminTenantId, false);
        var context = CreateContext(TenantConstants.SuperAdminTenantId, userId);

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
        await _cache.DidNotReceive().GetAsync<bool?>($"Tenants:active:{TenantConstants.SuperAdminTenantId}");
    }

    [Fact]
    public async Task Returns_403_InactiveTenant_When_tenant_is_inactive()
    {
        var userId = Guid.NewGuid();
        SetTenantActive(RegularTenantId, false);
        SetUserActive(userId, true);
        var context = CreateContext(RegularTenantId, userId);

        await CreateSut().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        ResponseCode(context).Should().Be(AuthErrors.InactiveTenant.Code);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Returns_403_InactiveUser_When_user_is_inactive()
    {
        var userId = Guid.NewGuid();
        SetTenantActive(RegularTenantId, true);
        SetUserActive(userId, false);
        var context = CreateContext(RegularTenantId, userId);

        await CreateSut().InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        ResponseCode(context).Should().Be(AuthErrors.InactiveUser.Code);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Calls_next_When_tenant_and_user_are_active()
    {
        var userId = Guid.NewGuid();
        SetTenantActive(RegularTenantId, true);
        SetUserActive(userId, true);
        var context = CreateContext(RegularTenantId, userId);

        await CreateSut().InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }
}
