using HrastERP.SharedKernel.Logging;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Users.Commands.ChangePassword;

public sealed record ChangePasswordCommand(Guid Id, [property: SensitiveData] string NewPassword) : IRequest<Result>;
