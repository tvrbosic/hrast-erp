using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(Guid Id, string Email, string FirstName, string LastName, Guid? RoleId) : IRequest<Result>;
