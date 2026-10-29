using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Roles.Commands.CreateRole;

internal sealed class CreateRoleCommandHandler(HrastDbContext dbContext)
    : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        // Invariant casing keeps the check independent of server culture (e.g. Turkish dotless i).
        var normalizedName = name.ToUpperInvariant();

        var nameTaken = await dbContext.Set<Role>()
            .AnyAsync(r => r.NormalizedName == normalizedName, cancellationToken);

        if (nameTaken)
            return AdministrationErrors.RoleNameTaken;

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = normalizedName,
            Description = request.Description ?? string.Empty,
            Permissions = PermissionMapper.ToFlags(request.Permissions)
        };

        dbContext.Set<Role>().Add(role);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Another request created the same name between the check above and this save.
            return AdministrationErrors.RoleNameTaken;
        }

        return role.Id;
    }
}
