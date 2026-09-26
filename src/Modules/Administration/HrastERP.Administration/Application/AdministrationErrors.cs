using HrastERP.SharedKernel.Results;

namespace HrastERP.Administration.Application;

public static class AdministrationErrors
{
    public static readonly Error TenantNotFound =
        Error.NotFound("Tenant.NotFound", "Tenant not found.");

    public static readonly Error TenantNameTaken =
        Error.Conflict("Tenant.NameTaken", "A tenant with this name already exists.");

    public static readonly Error SuperAdminTenantRequired =
        Error.Forbidden("Tenant.SuperAdminRequired", "Only super admin tenant users can perform this operation.");

    public static readonly Error UserNotFound =
        Error.NotFound("User.NotFound", "User not found.");

    public static readonly Error UserEmailTaken =
        Error.Conflict("User.EmailTaken", "A user with this email already exists.");

    public static readonly Error UserTenantMismatch =
        Error.Forbidden("User.TenantMismatch", "You can only manage users within your own tenant.");

    public static readonly Error UserCreationFailed =
        Error.Unexpected("User.CreationFailed", "Failed to create user.");

    public static readonly Error RoleNotFound =
        Error.NotFound("Role.NotFound", "The specified role does not exist.");

    public static readonly Error TenantInactive =
        Error.Validation("Tenant.Inactive", "Cannot create a user for an inactive tenant.");
}
