namespace HrastERP.SharedKernel.Domain;

/// <summary>
/// An entity whose created/updated fields are filled by <c>AuditableEntityInterceptor</c> on save
/// (UTC; user id is <see cref="Guid.Empty"/> when no user is authenticated). Changes to it are also
/// written to the audit log by <c>AuditLogInterceptor</c>.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; }
    Guid CreatedBy { get; }
    DateTime? UpdatedAt { get; }
    Guid? UpdatedBy { get; }
}
