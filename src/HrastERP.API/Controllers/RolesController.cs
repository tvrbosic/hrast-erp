using HrastERP.Administration.Application.Roles.Commands.CreateRole;
using HrastERP.Administration.Application.Roles.Commands.DeleteRole;
using HrastERP.Administration.Application.Roles.Commands.UpdateRole;
using HrastERP.Administration.Application.Roles.Queries.GetRoleById;
using HrastERP.Administration.Application.Roles.Queries.GetRoles;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.SharedKernel.Authorization;
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
    public async Task<IActionResult> Create(CreateRoleCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequireSuperAdminTenant]
    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRoleCommand command, CancellationToken ct)
    {
        if (id != command.Id)
            return BadRequest();

        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequireSuperAdminTenant]
    [RequirePermission(Permission.AdministrationDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteRoleCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRoleByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetRolesQuery(page, pageSize), ct);
        return result.ToActionResult();
    }
}
