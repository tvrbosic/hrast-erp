namespace HrastERP.SharedKernel.Domain;

public abstract class AuditableAggregateRoot<TId> : AggregateRoot<TId>, IAuditable
    where TId : notnull
{
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    protected AuditableAggregateRoot(TId id) : base(id) { }

    protected AuditableAggregateRoot() { } // Required for EF Core
}
