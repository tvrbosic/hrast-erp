namespace HrastERP.Administration.Application.DTOs;

public sealed record PermissionGroupResponse(
    string Module, IReadOnlyList<string> Permissions);
