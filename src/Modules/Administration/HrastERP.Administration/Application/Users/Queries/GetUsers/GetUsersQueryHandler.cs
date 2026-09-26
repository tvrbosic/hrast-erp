using HrastERP.Administration.Application.DTOs;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users.Queries.GetUsers;

internal sealed class GetUsersQueryHandler(HrastDbContext dbContext)
    : IRequestHandler<GetUsersQuery, Result<PagedResult<UserResponse>>>
{
    public async Task<Result<PagedResult<UserResponse>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Include(u => u.Role)
            .AsQueryable();

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        if (request.RoleId.HasValue)
            query = query.Where(u => u.RoleId == request.RoleId.Value);

        var joinedQuery = query.Join(
            dbContext.Set<Tenant>(),
            u => u.TenantId,
            t => t.Id,
            (u, t) => new { User = u, TenantName = t.Name });

        var totalCount = await joinedQuery.CountAsync(cancellationToken);

        var items = await joinedQuery
            .OrderBy(x => x.User.LastName)
            .ThenBy(x => x.User.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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
            .ToListAsync(cancellationToken);

        return PagedResult<UserResponse>.Create(items, totalCount, request.Page, request.PageSize);
    }
}
