using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Tenants.Commands.DeleteTenant;

public sealed record DeleteTenantCommand(Guid Id) : IRequest<Result>;
