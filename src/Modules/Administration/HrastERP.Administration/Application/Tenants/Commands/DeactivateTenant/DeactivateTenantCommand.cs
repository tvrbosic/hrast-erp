using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Tenants.Commands.DeactivateTenant;

public sealed record DeactivateTenantCommand(Guid Id) : IRequest<Result>;
