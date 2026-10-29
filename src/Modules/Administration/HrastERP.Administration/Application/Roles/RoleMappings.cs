using HrastERP.Administration.Application.DTOs;
using HrastERP.Infrastructure.Authorization;

namespace HrastERP.Administration.Application.Roles;

internal static class RoleMappings
{
    // Runs in memory (after materialization) — PermissionMapper can't be translated to SQL.
    public static RoleResponse ToResponse(this Role role) =>
        new(role.Id, role.Name, role.Description, PermissionMapper.ToNames(role.Permissions));
}
