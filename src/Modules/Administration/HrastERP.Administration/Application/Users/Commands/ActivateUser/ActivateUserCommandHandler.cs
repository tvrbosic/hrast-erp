using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users.Commands.ActivateUser;

internal sealed class ActivateUserCommandHandler(HrastDbContext dbContext, ICacheService cacheService)
    : IRequestHandler<ActivateUserCommand, Result>
{
    public async Task<Result> Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Set<ApplicationUser>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(AdministrationErrors.UserNotFound);

        user.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        await cacheService.RemoveAsync($"Users:active:{request.Id}", cancellationToken);
        return Result.Success();
    }
}
