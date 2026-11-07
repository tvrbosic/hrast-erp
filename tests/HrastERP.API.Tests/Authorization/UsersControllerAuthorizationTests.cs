using System.Reflection;
using FluentAssertions;
using HrastERP.API.Authorization;
using HrastERP.API.Controllers;
using HrastERP.SharedKernel.Authorization;

namespace HrastERP.API.Tests.Authorization;

public class UsersControllerAuthorizationTests
{
    [Fact]
    public void Requires_AdministrationDelete_When_deleting_user()
    {
        typeof(UsersController).GetMethod(nameof(UsersController.Delete), BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttributes<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .Should().Equal(Permission.AdministrationDelete);
    }
}
