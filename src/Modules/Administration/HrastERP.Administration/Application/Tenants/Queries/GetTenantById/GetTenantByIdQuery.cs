using HrastERP.Administration.Application.DTOs;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Tenants.Queries.GetTenantById;

public sealed record GetTenantByIdQuery(Guid Id) : IRequest<Result<TenantResponse>>;
