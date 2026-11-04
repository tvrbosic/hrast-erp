using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Roles.Commands.DeleteRole;

internal sealed class DeleteRoleCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<DeleteRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == RoleConstants.AdministratorRoleId)
            return Result.Failure(AdministrationErrors.RoleAdministratorImmutable);

        var role = await dbContext.Set<Role>()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
            return Result.Failure(AdministrationErrors.RoleNotFound);

        var assignedUserCount = await CountAssignedUsersAsync(request.Id, cancellationToken);

        if (assignedUserCount > 0)
            return Result.Failure(AdministrationErrors.RoleInUse(assignedUserCount));

        // Role isn't ISoftDeletable, so this is a real DELETE.
        dbContext.Set<Role>().Remove(role);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // Roles are global, so count users across all tenants (and inactive ones) —
    // the tenant query filter would otherwise hide some of them.
    private Task<int> CountAssignedUsersAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.Set<ApplicationUser>()
            .IgnoreQueryFilters()
            .CountAsync(u => u.RoleId == roleId, cancellationToken);
}
