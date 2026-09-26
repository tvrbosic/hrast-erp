using HrastERP.Administration.Application.DTOs;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Users.Queries.GetUsers;

public sealed record GetUsersQuery(int Page, int PageSize, bool? IsActive, Guid? RoleId) : IRequest<Result<PagedResult<UserResponse>>>;
