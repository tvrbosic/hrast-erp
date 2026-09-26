using HrastERP.Administration.Application.DTOs;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Queries.GetTenants;

internal sealed class GetTenantsQueryHandler(HrastDbContext dbContext)
    : IRequestHandler<GetTenantsQuery, Result<PagedResult<TenantResponse>>>
{
    public async Task<Result<PagedResult<TenantResponse>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Tenant>().AsNoTracking();

        if (request.IsActive.HasValue)
            query = query.Where(t => t.IsActive == request.IsActive.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TenantResponse(
                t.Id,
                t.Name,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt))
            .ToListAsync(cancellationToken);

        return PagedResult<TenantResponse>.Create(items, totalCount, request.Page, request.PageSize);
    }
}
