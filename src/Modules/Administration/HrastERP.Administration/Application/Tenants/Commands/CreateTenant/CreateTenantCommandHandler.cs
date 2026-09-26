using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Commands.CreateTenant;

internal sealed class CreateTenantCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<CreateTenantCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var nameExists = await dbContext.Set<Tenant>()
            .AnyAsync(t => t.Name == request.Name, cancellationToken);

        if (nameExists)
            return AdministrationErrors.TenantNameTaken;

        var tenant = new Tenant(Guid.NewGuid(), request.Name);

        dbContext.Set<Tenant>().Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }
}
