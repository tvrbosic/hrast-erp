namespace HrastERP.SharedKernel.Domain;

public abstract class SoftDeletableEntity<TId> : BaseEntity<TId>, ISoftDeletable
    where TId : notnull
{
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    protected SoftDeletableEntity(TId id) : base(id) { }

    protected SoftDeletableEntity() { } // Required for EF Core
}
