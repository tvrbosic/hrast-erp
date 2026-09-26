using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Tenants.Commands.ActivateTenant;

public sealed record ActivateTenantCommand(Guid Id) : IRequest<Result>;
