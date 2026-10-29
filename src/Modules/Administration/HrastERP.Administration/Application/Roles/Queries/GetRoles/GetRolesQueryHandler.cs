using HrastERP.Administration.Application.DTOs;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Roles.Queries.GetRoles;

internal sealed class GetRolesQueryHandler(HrastDbContext dbContext)
    : IRequestHandler<GetRolesQuery, Result<PagedResult<RoleResponse>>>
{
    public async Task<Result<PagedResult<RoleResponse>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Role>().AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var roles = await query
            .OrderBy(r => r.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = roles.Select(r => r.ToResponse());

        return PagedResult<RoleResponse>.Create(items, totalCount, request.Page, request.PageSize);
    }
}
