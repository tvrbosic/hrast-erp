namespace HrastERP.Administration.Application.DTOs;

public sealed record TenantResponse(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
