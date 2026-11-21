using HrastERP.Administration.Application.Roles.Commands.CreateRole;
using HrastERP.Administration.Application.Roles.Commands.DeleteRole;
using HrastERP.Administration.Application.Roles.Commands.UpdateRole;
using HrastERP.Administration.Application.Roles.Models;
using HrastERP.Administration.Application.Roles.Queries.GetRoleById;
using HrastERP.Administration.Application.Roles.Queries.GetRoles;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Controllers;

// Roles are global: any tenant admin may read them (to assign roles to users),
// but only super admin tenant users may change them.
[ApiController]
[Route("api/admin/roles")]
public sealed class RolesController(ISender sender) : ControllerBase
{
    [RequireSuperAdminTenant]
    [RequirePermission(Permission.AdministrationCreate)]
    [HttpPost]
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateRoleCommand command, CancellationToken ct)
    {
        // The result of a create is the id (Guid) of the newly created role; ToCreatedResult uses
        // result.Value to build the Location header pointing at GetById for that id
        var result = await sender.Send(command, ct);
        return result.ToCreatedResult(nameof(GetById));
    }

    [RequireSuperAdminTenant]
    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, UpdateRoleCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return Result.Failure(ApiErrors.RouteIdMismatch).ToActionResult();

        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequireSuperAdminTenant]
    [RequirePermission(Permission.AdministrationDelete)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteRoleCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRoleByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyCollection<RoleResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetRolesQuery(page, pageSize), ct);
        return result.ToActionResult();
    }
}
