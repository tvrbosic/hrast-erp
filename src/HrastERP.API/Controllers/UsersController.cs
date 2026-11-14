using HrastERP.Administration.Application.Users.Commands.ActivateUser;
using HrastERP.Administration.Application.Users.Commands.ChangePassword;
using HrastERP.Administration.Application.Users.Commands.CreateUser;
using HrastERP.Administration.Application.Users.Commands.DeactivateUser;
using HrastERP.Administration.Application.Users.Commands.DeleteUser;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using HrastERP.Administration.Application.Users.Models;
using HrastERP.Administration.Application.Users.Queries.GetUserById;
using HrastERP.Administration.Application.Users.Queries.GetUsers;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Controllers;

[ApiController]
[Route("api/admin/users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [RequirePermission(Permission.AdministrationCreate)]
    [HttpPost]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateUserCommand command, CancellationToken ct)
    {
        // The result of a create is the id (Guid) of the newly created user; ToCreatedResult uses
        // result.Value to build the Location header pointing at GetById for that id
        var result = await sender.Send(command, ct);
        return result.ToCreatedResult(nameof(GetById));
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, UpdateUserCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return Result.Failure(ApiErrors.RouteIdMismatch).ToActionResult();

        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivateUserCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateUserCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPatch("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword(Guid id, ChangePasswordCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return Result.Failure(ApiErrors.RouteIdMismatch).ToActionResult();

        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationDelete)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteUserCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyCollection<UserResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
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
