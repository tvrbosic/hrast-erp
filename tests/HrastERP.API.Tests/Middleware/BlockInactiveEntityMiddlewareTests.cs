using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using HrastERP.API.Middleware;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.API.Tests.Middleware;

// Statuses are served from the cache substitute, so most tests need no database.
public class BlockInactiveEntityMiddlewareTests
{
    private static readonly Guid RegularTenantId = Guid.NewGuid();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private bool _nextCalled;

    private const int ActiveStatusTtlMinutes = 7;

    private BlockInactiveEntityMiddleware CreateSut(IServiceScopeFactory? scopeFactory = null) => new(
        _ => { _nextCalled = true; return Task.CompletedTask; },
        _cache,
        scopeFactory ?? Substitute.For<IServiceScopeFactory>(),
        Options.Create(new CacheSettings { ConnectionString = "unused", ActiveStatusTtlMinutes = ActiveStatusTtlMinutes }),
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

    [Fact]
    public async Task Caches_user_status_with_configured_TTL_When_status_is_not_cached()
    {
        // Arrange: store one active user in an InMemory database reachable through the scope factory;
        // the cache substitute returns null, so the middleware has to load the status from there
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);
        var services = new ServiceCollection();
        services.AddSingleton(currentTenant);
        // Name captured once: the options lambda runs per scope, and each scope must see the same database
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<HrastDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();

        var userId = Guid.NewGuid();
        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HrastDbContext>();
            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                Email = "user@example.com",
                UserName = "user@example.com",
                TenantId = TenantConstants.SuperAdminTenantId,
                FirstName = "Test",
                LastName = "User",
                IsActive = true
            });
            await dbContext.SaveChangesAsync();
        }

        // Act: request as that user in the super admin tenant, which skips the tenant lookup
        var context = CreateContext(TenantConstants.SuperAdminTenantId, userId);
        await CreateSut(provider.GetRequiredService<IServiceScopeFactory>()).InvokeAsync(context);

        // Assert: the loaded status is cached for the configured number of minutes
        await _cache.Received(1).SetAsync($"Users:active:{userId}", true, TimeSpan.FromMinutes(ActiveStatusTtlMinutes));
        _nextCalled.Should().BeTrue();
    }
}
