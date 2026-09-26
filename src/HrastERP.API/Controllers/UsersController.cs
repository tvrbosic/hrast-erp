using HrastERP.Administration.Application.Users.Commands.ActivateUser;
using HrastERP.Administration.Application.Users.Commands.ChangePassword;
using HrastERP.Administration.Application.Users.Commands.CreateUser;
using HrastERP.Administration.Application.Users.Commands.DeactivateUser;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using HrastERP.Administration.Application.Users.Queries.GetUserById;
using HrastERP.Administration.Application.Users.Queries.GetUsers;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.SharedKernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Controllers;

[ApiController]
[Route("api/admin/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [RequirePermission(Permission.AdministrationCreate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest();

        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivateUserCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateUserCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPatch("{id:guid}/password")]
    public async Task<IActionResult> ChangePassword(Guid id, ChangePasswordCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest();

        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? roleId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetUsersQuery(page, pageSize, isActive, roleId), ct);
        return result.ToActionResult();
    }
}
