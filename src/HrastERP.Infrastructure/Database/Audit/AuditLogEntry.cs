using HrastERP.SharedKernel.Domain;

namespace HrastERP.Infrastructure.Database.Audit;

/// <summary>
/// One row of the append-only <c>audit_logs</c> table, written by <see cref="AuditLogInterceptor"/>
/// and the soft-delete cleanup job.
/// </summary>
public sealed class AuditLogEntry : ITenantEntity
{
    public Guid Id { get; set; }
    /// <summary>C# class name of the changed entity (e.g. <c>"Tenant"</c>).</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primary key as text; a JSON array for composite keys.</summary>
    public string EntityId { get; set; } = string.Empty;

    public AuditAction Action { get; set; }
    /// <summary>JSON object keyed by database column name; only changed columns for updates.</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON object keyed by database column name; only changed columns for updates.</summary>
    public string? NewValues { get; set; }

    /// <summary><see cref="Guid.Empty"/> for changes made without a user (system jobs, login).</summary>
    public Guid UserId { get; set; }

    /// <summary>Tenant that owns the changed data, not necessarily the tenant of the user who changed it.</summary>
    public Guid TenantId { get; set; }

    public DateTime Timestamp { get; set; }
}
