using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.Administration.Application.Users.Commands.CreateUser;

internal sealed class CreateUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    HrastDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
    : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = currentTenant.TenantId == TenantConstants.SuperAdminTenantId;
        var targetTenantId = isSuperAdmin && request.TenantId.HasValue
            ? request.TenantId.Value
            : currentTenant.TenantId;

        var tenant = await dbContext.Set<Tenant>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == targetTenantId && t.DeletedAt == null, cancellationToken);

        if (tenant is null)
            return AdministrationErrors.TenantNotFound;

        if (!tenant.IsActive)
            return AdministrationErrors.TenantInactive;

        // Identity's own duplicate check goes through the query filters, so it can't see users of other
        // tenants or soft-deleted users, which still hold the unique user name until they are purged.
        var normalizedEmail = userManager.NormalizeEmail(request.Email);
        var normalizedName = userManager.NormalizeName(request.Email);
        if (await dbContext.Set<ApplicationUser>()
                .IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedUserName == normalizedName,
                    cancellationToken))
            return AdministrationErrors.UserEmailTaken;

        if (request.RoleId.HasValue)
        {
            var rolePermissions = await RoleAssignment.GetPermissionsAsync(
                dbContext, request.RoleId.Value, cancellationToken);

            if (rolePermissions is null)
                return AdministrationErrors.RoleNotFound;

            if (!isSuperAdmin && RoleAssignment.Exceeds(rolePermissions.Value, currentUser.EffectivePermissions))
                return AdministrationErrors.RoleExceedsOwnPermissions;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TenantId = targetTenantId,
            RoleId = request.RoleId,
            IsActive = true
        };

        var identityResult = await userManager.CreateAsync(user, request.Password);

        if (!identityResult.Succeeded)
        {
            var errors = identityResult.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())
                as IReadOnlyDictionary<string, string[]>;

            return Error.Validation("User.CreationFailed", "Failed to create user.", errors);
        }

        return user.Id;
    }
}
