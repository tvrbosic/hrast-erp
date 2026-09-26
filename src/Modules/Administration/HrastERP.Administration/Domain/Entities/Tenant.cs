using HrastERP.SharedKernel.Domain;

namespace HrastERP.Administration.Domain.Entities;

public sealed class Tenant : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public Tenant(Guid id, string name) : base(id)
    {
        Name = name;
    }

    private Tenant() { } // Required for EF Core

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
