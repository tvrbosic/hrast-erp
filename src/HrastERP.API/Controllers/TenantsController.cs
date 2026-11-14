using HrastERP.Administration.Application.DTOs;
using HrastERP.Administration.Application.Tenants.Commands.ActivateTenant;
using HrastERP.Administration.Application.Tenants.Commands.CreateTenant;
using HrastERP.Administration.Application.Tenants.Commands.DeactivateTenant;
using HrastERP.Administration.Application.Tenants.Commands.DeleteTenant;
using HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;
using HrastERP.Administration.Application.Tenants.Queries.GetTenantById;
using HrastERP.Administration.Application.Tenants.Queries.GetTenants;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Results;
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
    [ProducesResponseType<ApiResponse<Guid>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateTenantCommand command, CancellationToken ct)
    {
        // The result of a create is the id (Guid) of the newly created tenant; ToCreatedResult uses
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
    public async Task<IActionResult> Update(Guid id, UpdateTenantCommand command, CancellationToken ct)
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
        var result = await sender.Send(new ActivateTenantCommand(id), ct);
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
        var result = await sender.Send(new DeactivateTenantCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationDelete)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteTenantCommand(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<TenantResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantByIdQuery(id), ct);
        return result.ToActionResult();
    }

    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyCollection<TenantResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
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
