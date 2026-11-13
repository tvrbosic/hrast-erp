namespace HrastERP.SharedKernel.Domain;

/// <summary>
/// Base for entities that raise domain events. Events are collected in <see cref="DomainEvents"/>
/// until <see cref="ClearDomainEvents"/> is called.
/// </summary>
public abstract class AggregateRoot<TId> : BaseEntity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(TId id) : base(id) { }

    protected AggregateRoot() { } // Required for EF Core

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
