using HrastERP.Administration.Application.DTOs;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Queries.GetTenantById;

internal sealed class GetTenantByIdQueryHandler(HrastDbContext dbContext)
    : IRequestHandler<GetTenantByIdQuery, Result<TenantResponse>>
{
    public async Task<Result<TenantResponse>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(t => new TenantResponse(
                t.Id,
                t.Name,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return tenant is not null
            ? tenant
            : AdministrationErrors.TenantNotFound;
    }
}
