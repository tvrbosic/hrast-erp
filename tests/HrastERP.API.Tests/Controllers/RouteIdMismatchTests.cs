using System.Reflection;
using FluentAssertions;
using HrastERP.Administration.Application.Roles.Commands.UpdateRole;
using HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;
using HrastERP.Administration.Application.Users.Commands.ChangePassword;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using HrastERP.API.Controllers;
using HrastERP.API.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace HrastERP.API.Tests.Controllers;

public class RouteIdMismatchTests
{
    // Hand-written because each case needs a real command instance with valid constructor
    // arguments. Covers_every_action_When_action_takes_route_id_and_body_id keeps it complete.
    public static TheoryData<string, Func<ISender, Guid, Guid, Task<IActionResult>>> Actions => new()
    {
        { "RolesController.Update", (s, routeId, bodyId) =>
            new RolesController(s).Update(routeId, new UpdateRoleCommand(bodyId, "Auditor", null, []), default) },
        { "TenantsController.Update", (s, routeId, bodyId) =>
            new TenantsController(s).Update(routeId, new UpdateTenantCommand(bodyId, "Acme"), default) },
        { "UsersController.Update", (s, routeId, bodyId) =>
            new UsersController(s).Update(routeId, new UpdateUserCommand(bodyId, "a@b.c", "A", "B", null), default) },
        { "UsersController.ChangePassword", (s, routeId, bodyId) =>
            new UsersController(s).ChangePassword(routeId, new ChangePasswordCommand(bodyId, "Secret123!"), default) },
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Returns_422_with_id_field_error_When_route_id_differs_from_body_id(
        string action, Func<ISender, Guid, Guid, Task<IActionResult>> invoke)
    {
        var sender = Substitute.For<ISender>();

        var actionResult = await invoke(sender, Guid.NewGuid(), Guid.NewGuid());

        var objectResult = actionResult.Should().BeAssignableTo<ObjectResult>(action).Subject;
        objectResult.StatusCode.Should().Be(422);
        var body = objectResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        body.Code.Should().Be("General.Validation");
        body.Errors.Should().ContainKey("id");
        sender.ReceivedCalls().Should().BeEmpty("the command must not be sent when the ids differ");
    }

    // The cases in Actions can't be generated, so a new action that takes a route id and a body id
    // would go untested unless someone remembers to add it. This guard finds those actions by
    // signature and fails until Actions lists exactly them: add a case for a new action, or remove
    // the case of one that no longer exists.
    [Fact]
    public void Covers_every_action_When_action_takes_route_id_and_body_id()
    {
        // AuthController only anchors the API assembly; every controller in it is scanned
        var discovered = typeof(AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(TakesRouteIdAndBodyId)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        var covered = Actions.Select(row => (string)row[0]);

        discovered.Should().BeEquivalentTo(covered);
    }

    // No HTTP-verb filter: UsersController.ChangePassword is a PATCH with both ids
    private static bool TakesRouteIdAndBodyId(MethodInfo action)
    {
        var parameters = action.GetParameters();
        return parameters.Any(p => p.Name == "id" && p.ParameterType == typeof(Guid))
            && parameters.Any(p => p.ParameterType.GetProperty("Id")?.PropertyType == typeof(Guid));
    }
}
