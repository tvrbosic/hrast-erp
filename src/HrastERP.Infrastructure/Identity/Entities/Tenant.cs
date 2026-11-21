using HrastERP.SharedKernel.Domain;

namespace HrastERP.Infrastructure.Identity;

public sealed class Tenant : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;

    /// <summary><c>Name.ToUpperInvariant()</c> (Name is stored trimmed); a unique index on it keeps names unique case-insensitively.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public Tenant(Guid id, string name) : base(id)
    {
        UpdateName(name);
    }

    private Tenant() { } // Required for EF Core

    public void UpdateName(string name)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
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
