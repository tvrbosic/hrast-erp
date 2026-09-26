using HrastERP.Administration.Application.DTOs;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users.Queries.GetUserById;

internal sealed class GetUserByIdQueryHandler(HrastDbContext dbContext)
    : IRequestHandler<GetUserByIdQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.Id == request.Id)
            .Join(
                dbContext.Set<Tenant>(),
                u => u.TenantId,
                t => t.Id,
                (u, t) => new { User = u, TenantName = t.Name })
            .Select(x => new UserResponse(
                x.User.Id,
                x.User.Email!,
                x.User.FirstName,
                x.User.LastName,
                x.User.IsActive,
                x.User.TenantId,
                x.TenantName,
                x.User.RoleId,
                x.User.Role != null ? x.User.Role.Name : null))
            .FirstOrDefaultAsync(cancellationToken);

        return user is not null ? user : AdministrationErrors.UserNotFound;
    }
}
