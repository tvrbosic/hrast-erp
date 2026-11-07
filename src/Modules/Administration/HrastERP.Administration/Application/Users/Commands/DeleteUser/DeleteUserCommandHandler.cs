using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users.Commands.DeleteUser;

internal sealed class DeleteUserCommandHandler(
    HrastDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    ICacheService cacheService)
    : IRequestHandler<DeleteUserCommand, Result>
{
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(AdministrationErrors.UserNotFound);

        if (user.Id == currentUser.UserId)
            return Result.Failure(AdministrationErrors.UserCannotDeleteSelf);

        if (currentTenant.TenantId != TenantConstants.SuperAdminTenantId && user.RoleId.HasValue)
        {
            var rolePermissions = await RoleAssignment.GetPermissionsAsync(
                dbContext, user.RoleId.Value, cancellationToken);

            if (rolePermissions is not null
                && RoleAssignment.Exceeds(rolePermissions.Value, currentUser.EffectivePermissions))
                return Result.Failure(AdministrationErrors.UserDeleteExceedsOwnPermissions);
        }

        user.IsActive = false;

        // The soft delete leaves the user row in place, so the FK cascade never removes its tokens.
        // Delete them explicitly so no refresh token outlives the user.
        var tokens = await dbContext.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Where(t => t.UserId == request.Id)
            .ToListAsync(cancellationToken);
        dbContext.Set<RefreshToken>().RemoveRange(tokens);

        dbContext.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await cacheService.RemoveAsync($"Users:active:{request.Id}", cancellationToken);
        return Result.Success();
    }
}
