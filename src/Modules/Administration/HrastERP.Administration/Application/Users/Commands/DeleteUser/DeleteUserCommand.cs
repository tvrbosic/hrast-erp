using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Users.Commands.DeleteUser;

public sealed record DeleteUserCommand(Guid Id) : IRequest<Result>;
