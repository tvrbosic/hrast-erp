using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Tenants.Commands.CreateTenant;

internal sealed class CreateTenantCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<CreateTenantCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        // Soft-deleted tenants keep their name (and its unique index entry) until the purge job removes them,
        // so ignore query filters to see them too.
        var normalizedName = request.Name.Trim().ToUpperInvariant();
        var nameExists = await dbContext.Set<Tenant>()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.NormalizedName == normalizedName, cancellationToken);

        if (nameExists)
            return AdministrationErrors.TenantNameTaken;

        var tenant = new Tenant(Guid.NewGuid(), request.Name);

        dbContext.Set<Tenant>().Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }
}
