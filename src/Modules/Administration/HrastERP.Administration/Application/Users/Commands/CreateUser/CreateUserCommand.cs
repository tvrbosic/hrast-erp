using HrastERP.SharedKernel.Logging;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    [property: SensitiveData] string Password,
    string FirstName,
    string LastName,
    Guid? TenantId,
    Guid? RoleId) : IRequest<Result<Guid>>;
