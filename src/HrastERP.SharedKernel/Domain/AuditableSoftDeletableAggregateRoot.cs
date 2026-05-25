namespace HrastERP.SharedKernel.Domain;

public abstract class AuditableSoftDeletableAggregateRoot<TId> : AggregateRoot<TId>, IAuditable, ISoftDeletable
    where TId : notnull
{
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    protected AuditableSoftDeletableAggregateRoot(TId id) : base(id) { }

    protected AuditableSoftDeletableAggregateRoot() { } // Required for EF Core
}
