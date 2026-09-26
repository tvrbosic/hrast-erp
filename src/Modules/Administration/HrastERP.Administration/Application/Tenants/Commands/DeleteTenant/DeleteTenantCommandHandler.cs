using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Commands.DeleteTenant;

internal sealed class DeleteTenantCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<DeleteTenantCommand, Result>
{
    public async Task<Result> Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(AdministrationErrors.TenantNotFound);

        dbContext.Set<Tenant>().Remove(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
