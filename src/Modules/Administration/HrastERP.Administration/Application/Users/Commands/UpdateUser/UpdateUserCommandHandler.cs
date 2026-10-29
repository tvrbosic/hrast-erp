using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    HrastDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(AdministrationErrors.UserNotFound);

        var isSuperAdmin = currentTenant.TenantId == TenantConstants.SuperAdminTenantId;
        var roleChanges = request.RoleId != user.RoleId;

        if (roleChanges && !isSuperAdmin && user.RoleId.HasValue)
        {
            // Can't demote (or strip) a user who holds permissions the caller lacks.
            var currentRolePermissions = await RoleAssignment.GetPermissionsAsync(
                dbContext, user.RoleId.Value, cancellationToken);

            if (currentRolePermissions is not null
                && RoleAssignment.Exceeds(currentRolePermissions.Value, currentUser.EffectivePermissions))
                return Result.Failure(AdministrationErrors.UserRoleExceedsOwnPermissions);
        }

        if (request.RoleId.HasValue)
        {
            var rolePermissions = await RoleAssignment.GetPermissionsAsync(
                dbContext, request.RoleId.Value, cancellationToken);

            if (rolePermissions is null)
                return Result.Failure(AdministrationErrors.RoleNotFound);

            if (roleChanges && !isSuperAdmin
                && RoleAssignment.Exceeds(rolePermissions.Value, currentUser.EffectivePermissions))
                return Result.Failure(AdministrationErrors.RoleExceedsOwnPermissions);
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.RoleId = request.RoleId;

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await userManager.SetEmailAsync(user, request.Email);
            if (!setEmailResult.Succeeded)
                return Result.Failure(AdministrationErrors.UserEmailTaken);

            await userManager.SetUserNameAsync(user, request.Email);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
