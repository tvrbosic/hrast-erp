namespace HrastERP.SharedKernel.Domain;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
    Guid? DeletedBy { get; }
}
