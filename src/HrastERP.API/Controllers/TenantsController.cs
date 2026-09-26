using HrastERP.Administration.Application.Tenants.Commands.ActivateTenant;
using HrastERP.Administration.Application.Tenants.Commands.CreateTenant;
using HrastERP.Administration.Application.Tenants.Commands.DeactivateTenant;
using HrastERP.Administration.Application.Tenants.Commands.DeleteTenant;
using HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;
using HrastERP.Administration.Application.Tenants.Queries.GetTenantById;
using HrastERP.Administration.Application.Tenants.Queries.GetTenants;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.SharedKernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Controllers;

[ApiController]
[Route("api/admin/tenants")]
[RequireSuperAdminTenant]
public sealed class TenantsController(ISender sender) : ControllerBase
{
    [RequirePermission(Permission.AdministrationCreate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateTenantCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTenantCommand command, CancellationToken ct)
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
        var result = await sender.Send(new ActivateTenantCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationEdit)]
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeactivateTenantCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteTenantCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetTenantsQuery(page, pageSize, isActive), ct);
        return result.ToActionResult();
    }
}
