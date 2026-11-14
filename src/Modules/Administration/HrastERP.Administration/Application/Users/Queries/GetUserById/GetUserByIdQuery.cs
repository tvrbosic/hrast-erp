using HrastERP.Administration.Application.Users.Models;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<Result<UserResponse>>;
