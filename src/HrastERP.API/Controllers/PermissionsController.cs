using HrastERP.Administration.Application.Roles.Models;
using HrastERP.Administration.Application.Roles.Queries.GetPermissions;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Controllers;

[ApiController]
[Route("api/admin/permissions")]
public sealed class PermissionsController(ISender sender) : ControllerBase
{
    [RequirePermission(Permission.AdministrationView)]
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PermissionGroupResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetPermissionsQuery(), ct);
        return result.ToActionResult();
    }
}
