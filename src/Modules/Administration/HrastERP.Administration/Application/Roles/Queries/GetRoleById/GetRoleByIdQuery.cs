using HrastERP.Administration.Application.Roles.Models;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Roles.Queries.GetRoleById;

public sealed record GetRoleByIdQuery(Guid Id) : IRequest<Result<RoleResponse>>;
