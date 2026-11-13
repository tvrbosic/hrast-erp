using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Authorization;

namespace HrastERP.Infrastructure.Authorization;

/// <summary>
/// A global role shared by all tenants. Unlike module entities it is not audited, soft-deletable or
/// tenant-scoped. A user has at most one role.
/// </summary>
public sealed class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary><c>Name.Trim().ToUpperInvariant()</c>; a unique index on it keeps names unique case-insensitively.</summary>
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Permission Permissions { get; set; }

    public ICollection<ApplicationUser> Users { get; set; } = [];
}
