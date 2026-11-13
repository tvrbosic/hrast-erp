namespace HrastERP.Infrastructure.Database.Audit;

/// <summary>
/// What happened to the entity in an <see cref="AuditLogEntry"/>. Stored by name, so renaming a member
/// breaks existing rows.
/// </summary>
public enum AuditAction
{
    Created,
    Updated,

    /// <summary>Soft-deleted (or hard-deleted, for entities that are not soft-deletable).</summary>
    Deleted,

    /// <summary>Permanently removed by the soft-delete cleanup job after the retention period.</summary>
    Purged
}
