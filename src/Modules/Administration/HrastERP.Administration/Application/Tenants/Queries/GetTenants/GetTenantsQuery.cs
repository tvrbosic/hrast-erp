using HrastERP.Administration.Application.Tenants.Models;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Administration.Application.Tenants.Queries.GetTenants;

public sealed record GetTenantsQuery(int Page, int PageSize, bool? IsActive) : IRequest<Result<PagedResult<TenantResponse>>>;
