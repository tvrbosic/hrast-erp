using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Commands.ActivateTenant;

internal sealed class ActivateTenantCommandHandler(HrastDbContext dbContext, ICacheService cacheService)
    : IRequestHandler<ActivateTenantCommand, Result>
{
    public async Task<Result> Handle(ActivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(AdministrationErrors.TenantNotFound);

        tenant.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);

        await cacheService.RemoveAsync($"Tenants:active:{request.Id}", cancellationToken);

        return Result.Success();
    }
}
