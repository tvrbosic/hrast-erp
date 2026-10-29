using HrastERP.Administration.Application.DTOs;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Roles.Queries.GetPermissions;

internal sealed class GetPermissionsQueryHandler
    : IRequestHandler<GetPermissionsQuery, Result<IReadOnlyList<PermissionGroupResponse>>>
{
    internal static readonly string[] ActionSuffixes = ["View", "Create", "Edit", "Delete"];

    public Task<Result<IReadOnlyList<PermissionGroupResponse>>> Handle(
        GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        // LINQ-to-objects GroupBy preserves first-occurrence order, and Defined is in bit order,
        // so groups and the permissions inside them come out in bit order.
        IReadOnlyList<PermissionGroupResponse> groups = PermissionMapper.Defined
            .Select(p => p.ToString())
            .GroupBy(ToModuleName)
            .Select(g => new PermissionGroupResponse(g.Key, g.ToList()))
            .ToList();

        // Explicit Success: implicit conversions don't apply when the source is an interface type.
        return Task.FromResult(Result<IReadOnlyList<PermissionGroupResponse>>.Success(groups));
    }

    // A permission with an unknown action (e.g. a future "FinanceApprove") becomes its own group
    // instead of failing the whole endpoint. A unit test flags such names so ActionSuffixes gets updated.
    internal static string ToModuleName(string permissionName)
    {
        var suffix = ActionSuffixes.FirstOrDefault(permissionName.EndsWith);
        return suffix is null ? permissionName : permissionName[..^suffix.Length];
    }
}
