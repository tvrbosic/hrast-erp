using HrastERP.Administration.Application.Roles.Models;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Roles.Queries.GetPermissions;

public sealed record GetPermissionsQuery : IRequest<Result<IReadOnlyList<PermissionGroupResponse>>>;
