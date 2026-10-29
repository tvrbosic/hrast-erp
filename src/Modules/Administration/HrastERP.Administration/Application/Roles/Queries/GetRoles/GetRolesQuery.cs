using HrastERP.Administration.Application.DTOs;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Roles.Queries.GetRoles;

public sealed record GetRolesQuery(int Page, int PageSize) : IRequest<Result<PagedResult<RoleResponse>>>;
