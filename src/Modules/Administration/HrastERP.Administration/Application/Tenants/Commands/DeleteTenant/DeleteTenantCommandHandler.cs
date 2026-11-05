using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
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

        // Users aren't soft-deletable, so they'd stay after the tenant is soft-deleted, and the purge job's
        // hard delete would then be refused by the users → tenants foreign key (OnDelete Restrict).
        // Ignore query filters so every user is counted, whatever the caller's context.
        var userCount = await dbContext.Set<ApplicationUser>()
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == request.Id, cancellationToken);

        if (userCount > 0)
            return Result.Failure(AdministrationErrors.TenantHasUsers(userCount));

        dbContext.Set<Tenant>().Remove(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
