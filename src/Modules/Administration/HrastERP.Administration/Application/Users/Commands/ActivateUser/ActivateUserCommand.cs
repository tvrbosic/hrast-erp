using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid Id) : IRequest<Result>;
