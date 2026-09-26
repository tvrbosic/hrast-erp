using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    HrastDbContext dbContext)
    : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(AdministrationErrors.UserNotFound);

        if (request.RoleId.HasValue)
        {
            var roleExists = await dbContext.Set<Role>()
                .AnyAsync(r => r.Id == request.RoleId.Value, cancellationToken);

            if (!roleExists)
                return Result.Failure(AdministrationErrors.RoleNotFound);
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
