using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users;

/// <summary>
/// Privilege ceiling for role assignment: outside the super admin tenant, an admin can only hand out
/// roles whose permissions they hold themselves, and can't change the role of a user who has
/// permissions they lack. Tenant scoping (own tenant only) is enforced separately by the handlers
/// and the tenant query filter.
/// </summary>
internal static class RoleAssignment
{
    public static bool Exceeds(Permission rolePermissions, Permission callerPermissions) =>
        (rolePermissions & ~callerPermissions) != Permission.None;

    // Null when the role doesn't exist.
    public static Task<Permission?> GetPermissionsAsync(
        HrastDbContext dbContext, Guid roleId, CancellationToken cancellationToken) =>
        dbContext.Set<Role>()
            .Where(r => r.Id == roleId)
            .Select(r => (Permission?)r.Permissions)
            .FirstOrDefaultAsync(cancellationToken);
}
