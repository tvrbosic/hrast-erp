using System.Reflection;
using FluentAssertions;
using HrastERP.API.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Tests.Controllers;

// Responses carrying tokens must not be cached by browsers or proxies, so these actions keep [ResponseCache]
// set to send "Cache-Control: no-store".
public class TokenResponseCacheTests
{
    [Theory]
    [InlineData(nameof(AuthController.Login))]
    [InlineData(nameof(AuthController.Refresh))]
    public void Disables_caching_When_action_returns_tokens(string action)
    {
        var attribute = typeof(AuthController)
            .GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<ResponseCacheAttribute>();

        attribute.Should().NotBeNull();
        attribute!.NoStore.Should().BeTrue();
        attribute.Location.Should().Be(ResponseCacheLocation.None);
    }
}
