namespace HrastERP.Infrastructure.Identity;

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid TenantId,
    string TenantName,
    Guid? RoleId,
    string? RoleName,
    IReadOnlyList<string> Permissions);
