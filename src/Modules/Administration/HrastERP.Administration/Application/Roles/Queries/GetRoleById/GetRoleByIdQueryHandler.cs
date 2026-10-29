using HrastERP.Administration.Application.DTOs;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Roles.Queries.GetRoleById;

internal sealed class GetRoleByIdQueryHandler(HrastDbContext dbContext)
    : IRequestHandler<GetRoleByIdQuery, Result<RoleResponse>>
{
    public async Task<Result<RoleResponse>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await dbContext.Set<Role>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
            return AdministrationErrors.RoleNotFound;

        return role.ToResponse();
    }
}
