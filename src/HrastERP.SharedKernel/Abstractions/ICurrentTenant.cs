namespace HrastERP.SharedKernel.Abstractions;

public interface ICurrentTenant
{
    Guid TenantId { get; }
}
