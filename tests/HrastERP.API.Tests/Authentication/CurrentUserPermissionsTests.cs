using System.Security.Claims;
using FluentAssertions;
using HrastERP.API.Authentication;
using HrastERP.SharedKernel.Authorization;
using Microsoft.AspNetCore.Http;

namespace HrastERP.API.Tests.Authentication;

public class CurrentUserPermissionsTests
{
    private static CurrentUser CreateCurrentUser(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.type, c.value)),
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        return new CurrentUser(accessor);
    }

    [Fact]
    public void EffectivePermissions_WithValidClaim_ReturnsCorrectPermission()
    {
        var expected = Permission.FinanceView | Permission.InventoryCreate;
        var sut = CreateCurrentUser(("permissions", ((long)expected).ToString()));

        sut.EffectivePermissions.Should().Be(expected);
    }

    [Fact]
    public void EffectivePermissions_WithMissingClaim_ReturnsNone()
    {
        var sut = CreateCurrentUser(); // no permissions claim

        sut.EffectivePermissions.Should().Be(Permission.None);
    }

    [Fact]
    public void EffectivePermissions_WithMalformedClaim_ReturnsNone()
    {
        var sut = CreateCurrentUser(("permissions", "not-a-number"));

        sut.EffectivePermissions.Should().Be(Permission.None);
    }

    [Fact]
    public void EffectivePermissions_WithZeroClaim_ReturnsNone()
    {
        var sut = CreateCurrentUser(("permissions", "0"));

        sut.EffectivePermissions.Should().Be(Permission.None);
    }

    [Fact]
    public void EffectivePermissions_WithNullHttpContext_ReturnsNone()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var sut = new CurrentUser(accessor);

        sut.EffectivePermissions.Should().Be(Permission.None);
    }
}
