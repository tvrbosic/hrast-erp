using HrastERP.SharedKernel.Results;

namespace HrastERP.Administration.Application;

public static class AdministrationErrors
{
    #region Tenant
    public static readonly Error SuperAdminTenantRequired =
        Error.Forbidden("Tenant.SuperAdminRequired", "Only super admin tenant users can perform this operation.");

    public static readonly Error TenantNotFound =
        Error.NotFound("Tenant.NotFound", "Tenant not found.");

    public static readonly Error TenantNameTaken =
        Error.Conflict("Tenant.NameTaken", "A tenant with this name already exists.");

    public static Error TenantHasUsers(int userCount) =>
        Error.Conflict("Tenant.HasUsers", $"Tenant has {userCount} users, including deleted users awaiting permanent removal. Remove them before deleting.");

    public static Error TenantHasFiles(int fileCount) =>
        Error.Conflict("Tenant.HasFiles", $"Tenant has {fileCount} stored files. Delete them before deleting.");

    public static readonly Error TenantInactive =
        Error.Validation("Tenant.Inactive", "Cannot create a user for an inactive tenant.");
    #endregion

    #region User
    public static readonly Error UserNotFound =
        Error.NotFound("User.NotFound", "User not found.");

    public static readonly Error UserEmailTaken =
        Error.Conflict("User.EmailTaken", "A user with this email already exists.");

    public static readonly Error UserTenantMismatch =
        Error.Forbidden("User.TenantMismatch", "You can only manage users within your own tenant.");

    public static readonly Error UserCreationFailed =
        Error.Unexpected("User.CreationFailed", "Failed to create user.");

    public static readonly Error UserRoleExceedsOwnPermissions =
        Error.Forbidden("User.RoleExceedsOwnPermissions", "You cannot change the role of a user who has permissions you do not have.");

    public static readonly Error UserCannotDeleteSelf =
        Error.Validation("User.CannotDeleteSelf", "You cannot delete your own account.");

    public static readonly Error UserDeleteExceedsOwnPermissions =
        Error.Forbidden("User.DeleteExceedsOwnPermissions", "You cannot delete a user who has permissions you do not have.");
    #endregion

    #region Role
    public static readonly Error RoleNotFound =
        Error.NotFound("Role.NotFound", "The specified role does not exist.");

    public static readonly Error RoleNameTaken =
        Error.Conflict("Role.NameTaken", "A role with this name already exists.");

    public static readonly Error RoleAdministratorImmutable =
        Error.Conflict("Role.AdministratorImmutable", "The Administrator role cannot be deleted and its permissions cannot be changed.");

    public static Error RoleInUse(int userCount) =>
        Error.Conflict("Role.InUse", $"Role is assigned to {userCount} users, including deleted users awaiting permanent removal. Reassign them before deleting.");

    public static readonly Error RoleExceedsOwnPermissions =
        Error.Forbidden("Role.ExceedsOwnPermissions", "You cannot assign a role with permissions you do not have.");

    #endregion


}
