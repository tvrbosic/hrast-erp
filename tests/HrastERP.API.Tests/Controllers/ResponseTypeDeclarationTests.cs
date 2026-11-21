using System.Reflection;
using FluentAssertions;
using HrastERP.API.Controllers;
using HrastERP.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    // [RequirePermission] and [RequireSuperAdminTenant] derive from AuthorizeAttribute, and
    // TenantsController puts [RequireSuperAdminTenant] on the class, so both places are checked.
    private static bool RequiresAuthorization(MethodInfo action) =>
        action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
        || action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();

    private static bool HasBindableInput(MethodInfo action) =>
        action.GetParameters().Any(p => p.GetCustomAttribute<FromQueryAttribute>() is not null || IsBody(p));

    // [ApiController] binds a complex type without a binding source attribute from the body.
    // Value types (Guid id, CancellationToken) and strings bind from route or query instead.
    private static bool IsBody(ParameterInfo parameter) =>
        parameter.GetCustomAttribute<FromBodyAttribute>() is not null
        || (!parameter.ParameterType.IsValueType
            && parameter.ParameterType != typeof(string)
            && !parameter.GetCustomAttributes().OfType<IBindingSourceMetadata>().Any());

    [Theory]
    [MemberData(nameof(ActionNames))]
    public void Declares_exactly_one_success_response_When_action_is_public(string action)
    {
        Declared(Find(action)).Count(a => a.StatusCode is >= 200 and < 300).Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ActionNames))]
    public void Declares_ErrorResponse_body_When_error_status_is_declared(string action)
    {
        Declared(Find(action))
            .Where(a => a.StatusCode >= 400 && a.Type != typeof(ErrorResponse))
            .Should().BeEmpty();
    }

    // 403 applies even to [Authorize]-only actions: BlockInactiveEntityMiddleware can return it
    // on any authenticated request.
    [Theory]
    [MemberData(nameof(ActionNames))]
    public void Declares_401_and_403_When_action_requires_authorization(string action)
    {
        var method = Find(action);
        if (!RequiresAuthorization(method))
            return;

        Declared(method).Select(a => a.StatusCode).Should().Contain(new[] { 401, 403 });
    }

    // Body and query input can fail model binding or validation (422 General.Validation); a bad
    // {id:guid} route value doesn't match the route at all, so route-only actions are skipped.
    [Theory]
    [MemberData(nameof(ActionNames))]
    public void Declares_422_When_action_binds_body_or_query(string action)
    {
        var method = Find(action);
        if (!HasBindableInput(method))
            return;

        Declared(method).Should().Contain(a => a.StatusCode == 422);
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
