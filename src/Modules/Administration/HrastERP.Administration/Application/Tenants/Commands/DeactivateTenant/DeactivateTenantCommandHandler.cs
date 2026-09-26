using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Commands.DeactivateTenant;

internal sealed class DeactivateTenantCommandHandler(HrastDbContext dbContext, ICacheService cacheService)
    : IRequestHandler<DeactivateTenantCommand, Result>
{
    public async Task<Result> Handle(DeactivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(AdministrationErrors.TenantNotFound);

        tenant.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        await cacheService.RemoveAsync($"Tenants:active:{request.Id}", cancellationToken);

        return Result.Success();
    }
}
