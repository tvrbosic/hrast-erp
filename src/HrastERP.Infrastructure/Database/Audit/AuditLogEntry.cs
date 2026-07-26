using HrastERP.SharedKernel.Domain;

namespace HrastERP.Infrastructure.Database.Audit;

public sealed class AuditLogEntry : ITenantEntity
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime Timestamp { get; set; }
}
