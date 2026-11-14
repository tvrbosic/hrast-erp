namespace HrastERP.Administration.Application.Tenants.Models;

public sealed record TenantResponse(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
