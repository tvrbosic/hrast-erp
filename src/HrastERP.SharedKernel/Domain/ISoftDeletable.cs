namespace HrastERP.SharedKernel.Domain;

/// <summary>
/// An entity that is never deleted directly. <c>SoftDeleteInterceptor</c> turns a delete into an update
/// of <see cref="DeletedAt"/> / <see cref="DeletedBy"/>, a global query filter hides deleted rows, and
/// <c>SoftDeleteCleanupJob</c> purges them after the retention period.
/// </summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
    Guid? DeletedBy { get; }
}
