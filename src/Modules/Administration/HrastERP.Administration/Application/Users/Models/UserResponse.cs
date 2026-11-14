namespace HrastERP.Administration.Application.Users.Models;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    Guid TenantId,
    string TenantName,
    Guid? RoleId,
    string? RoleName);
