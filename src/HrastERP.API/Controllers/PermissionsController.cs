using HrastERP.Administration.Application.Roles.Queries.GetPermissions;
using HrastERP.API.Authorization;
using HrastERP.API.Extensions;
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
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetPermissionsQuery(), ct);
        return result.ToActionResult();
    }
}
