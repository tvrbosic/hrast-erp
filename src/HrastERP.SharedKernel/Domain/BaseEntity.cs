namespace HrastERP.SharedKernel.Domain;

public abstract class BaseEntity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected BaseEntity(TId id)
    {
        Id = id;
    }

    protected BaseEntity() { } // Required for EF Core

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(BaseEntity<TId>? a, BaseEntity<TId>? b)
    {
        if (a is null && b is null)
            return true;

        if (a is null || b is null)
            return false;

        return a.Equals(b);
    }

    public static bool operator !=(BaseEntity<TId>? a, BaseEntity<TId>? b) => !(a == b);
}
