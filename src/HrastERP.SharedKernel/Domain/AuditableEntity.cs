namespace HrastERP.SharedKernel.Domain;

public abstract class AuditableEntity<TId> : BaseEntity<TId>, IAuditable
    where TId : notnull
{
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    protected AuditableEntity(TId id) : base(id) { }

    protected AuditableEntity() { } // Required for EF Core
}
