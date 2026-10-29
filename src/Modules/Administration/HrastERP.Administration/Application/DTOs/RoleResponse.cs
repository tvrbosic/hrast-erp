namespace HrastERP.Administration.Application.DTOs;

public sealed record RoleResponse (
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);
