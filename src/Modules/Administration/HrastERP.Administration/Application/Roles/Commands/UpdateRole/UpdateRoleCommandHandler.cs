using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Roles.Commands.UpdateRole;

internal sealed class UpdateRoleCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<UpdateRoleCommand, Result>
{
    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await dbContext.Set<Role>()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
            return Result.Failure(AdministrationErrors.RoleNotFound);

        var name = request.Name.Trim();
        // Invariant casing keeps the check independent of server culture (e.g. Turkish dotless i).
        var normalizedName = name.ToUpperInvariant();

        var nameTaken = await dbContext.Set<Role>()
            .AnyAsync(r => r.Id != request.Id && r.NormalizedName == normalizedName, cancellationToken);

        if (nameTaken)
            return Result.Failure(AdministrationErrors.RoleNameTaken);

        var permissions = PermissionMapper.ToFlags(request.Permissions);

        // The Administrator role must always hold every permission, otherwise
        // nobody could manage roles or tenants anymore (lockout).
        if (role.Id == RoleConstants.AdministratorRoleId && permissions != PermissionMapper.All)
            return Result.Failure(AdministrationErrors.RoleAdministratorImmutable);

        role.Name = name;
        role.NormalizedName = normalizedName;
        role.Description = request.Description ?? string.Empty;
        role.Permissions = permissions;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Another request took the name between the check above and this save.
            return Result.Failure(AdministrationErrors.RoleNameTaken);
        }

        return Result.Success();
    }
}
