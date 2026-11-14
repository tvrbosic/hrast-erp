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
    public static TheoryData<string, Func<ISender, Guid, Guid, Task<IActionResult>>> Actions => new()
    {
        { "Roles.Update", (s, routeId, bodyId) =>
            new RolesController(s).Update(routeId, new UpdateRoleCommand(bodyId, "Auditor", null, []), default) },
        { "Tenants.Update", (s, routeId, bodyId) =>
            new TenantsController(s).Update(routeId, new UpdateTenantCommand(bodyId, "Acme"), default) },
        { "Users.Update", (s, routeId, bodyId) =>
            new UsersController(s).Update(routeId, new UpdateUserCommand(bodyId, "a@b.c", "A", "B", null), default) },
        { "Users.ChangePassword", (s, routeId, bodyId) =>
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
}
