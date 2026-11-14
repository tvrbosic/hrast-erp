using System.Reflection;
using FluentAssertions;
using HrastERP.API.Controllers;
using HrastERP.API.Responses;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Tests.Controllers;

// Actions return IActionResult, so OpenAPI only knows their responses from [ProducesResponseType].
// These tests keep those declarations in line with what ResultExtensions actually returns.
public class ResponseTypeDeclarationTests
{
    private static readonly Type[] Controllers =
    [
        typeof(AuthController), typeof(PermissionsController), typeof(RolesController),
        typeof(TenantsController), typeof(UsersController)
    ];

    private static IEnumerable<MethodInfo> AllActions() =>
        Controllers.SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

    private static MethodInfo Action(Type controller, string name) =>
        controller.GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;

    private static ProducesResponseTypeAttribute[] Declared(MethodInfo action) =>
        action.GetCustomAttributes<ProducesResponseTypeAttribute>().ToArray();

    public static TheoryData<string> ActionNames()
    {
        var data = new TheoryData<string>();
        foreach (var action in AllActions())
            data.Add($"{action.DeclaringType!.Name}.{action.Name}");
        return data;
    }

    private static MethodInfo Find(string qualifiedName) =>
        AllActions().Single(a => $"{a.DeclaringType!.Name}.{a.Name}" == qualifiedName);

    [Theory]
    [MemberData(nameof(ActionNames))]
    public void Declares_exactly_one_success_response_When_action_is_public(string action)
    {
        Declared(Find(action)).Count(a => a.StatusCode is >= 200 and < 300).Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ActionNames))]
    public void Declares_ErrorResponse_body_When_error_status_other_than_401_is_declared(string action)
    {
        Declared(Find(action))
            .Where(a => a.StatusCode >= 400 && a.StatusCode != 401 && a.Type != typeof(ErrorResponse))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(RolesController))]
    [InlineData(typeof(TenantsController))]
    [InlineData(typeof(UsersController))]
    public void Declares_201_with_id_envelope_When_action_creates_resource(Type controller)
    {
        Declared(Action(controller, "Create")).Should()
            .Contain(a => a.StatusCode == 201 && a.Type == typeof(ApiResponse<Guid>));
    }

    [Theory]
    [InlineData(typeof(AuthController), "Logout")]
    [InlineData(typeof(RolesController), "Update")]
    [InlineData(typeof(RolesController), "Delete")]
    [InlineData(typeof(TenantsController), "Update")]
    [InlineData(typeof(TenantsController), "Activate")]
    [InlineData(typeof(TenantsController), "Deactivate")]
    [InlineData(typeof(TenantsController), "Delete")]
    [InlineData(typeof(UsersController), "Update")]
    [InlineData(typeof(UsersController), "Activate")]
    [InlineData(typeof(UsersController), "Deactivate")]
    [InlineData(typeof(UsersController), "ChangePassword")]
    [InlineData(typeof(UsersController), "Delete")]
    public void Declares_204_without_body_When_handler_returns_no_value(Type controller, string action)
    {
        Declared(Action(controller, action)).Should()
            .Contain(a => a.StatusCode == 204 && a.Type == typeof(void));
    }
}
