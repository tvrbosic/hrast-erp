using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;

public sealed record UpdateTenantCommand(Guid Id, string Name) : IRequest<Result>;
