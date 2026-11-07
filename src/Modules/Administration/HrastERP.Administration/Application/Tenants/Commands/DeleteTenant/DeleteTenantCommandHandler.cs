using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.FileStorage;
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

        // Soft-deleted users still hold a row until the purge job removes them, so they'd outlive the tenant and
        // the purge job's hard delete would then be refused by the users → tenants foreign key (OnDelete Restrict).
        // Ignore query filters so every user, soft-deleted ones included, is counted whatever the caller's context.
        var userCount = await dbContext.Set<ApplicationUser>()
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == request.Id, cancellationToken);

        if (userCount > 0)
            return Result.Failure(AdministrationErrors.TenantHasUsers(userCount));

        // Files have no foreign key to the tenant, so nothing in the database would stop them being orphaned.
        // Soft-deleted files don't count: the purge job removes them anyway.
        var fileCount = await dbContext.Set<StoredFile>()
            .IgnoreQueryFilters()
            .CountAsync(f => f.TenantId == request.Id && f.DeletedAt == null, cancellationToken);

        if (fileCount > 0)
            return Result.Failure(AdministrationErrors.TenantHasFiles(fileCount));

        dbContext.Set<Tenant>().Remove(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
