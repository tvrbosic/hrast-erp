using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Tenants.Commands.CreateTenant;

public sealed record CreateTenantCommand(string Name) : IRequest<Result<Guid>>;
