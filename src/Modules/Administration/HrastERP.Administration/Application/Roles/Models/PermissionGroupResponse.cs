namespace HrastERP.Administration.Application.Roles.Models;

public sealed record PermissionGroupResponse(
    string Module, IReadOnlyList<string> Permissions);
