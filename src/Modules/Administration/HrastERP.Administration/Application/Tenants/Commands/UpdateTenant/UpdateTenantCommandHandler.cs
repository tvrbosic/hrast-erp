using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;

internal sealed class UpdateTenantCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<UpdateTenantCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(AdministrationErrors.TenantNotFound);

        var nameExists = await dbContext.Set<Tenant>()
            .AnyAsync(t => t.Name == request.Name && t.Id != request.Id, cancellationToken);

        if (nameExists)
            return Result.Failure(AdministrationErrors.TenantNameTaken);

        tenant.UpdateName(request.Name);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
