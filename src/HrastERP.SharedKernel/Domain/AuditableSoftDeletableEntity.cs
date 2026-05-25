namespace HrastERP.SharedKernel.Domain;

public abstract class AuditableSoftDeletableEntity<TId> : BaseEntity<TId>, IAuditable, ISoftDeletable
    where TId : notnull
{
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    protected AuditableSoftDeletableEntity(TId id) : base(id) { }

    protected AuditableSoftDeletableEntity() { } // Required for EF Core
}
