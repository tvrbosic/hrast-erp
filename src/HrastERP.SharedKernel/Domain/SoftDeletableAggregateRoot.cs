namespace HrastERP.SharedKernel.Domain;

public abstract class SoftDeletableAggregateRoot<TId> : AggregateRoot<TId>, ISoftDeletable
    where TId : notnull
{
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    protected SoftDeletableAggregateRoot(TId id) : base(id) { }

    protected SoftDeletableAggregateRoot() { } // Required for EF Core
}
