using HrastERP.SharedKernel.Authorization;

namespace HrastERP.Administration.Application.Roles;

/// <summary>
/// Converts between the API representation of permissions (enum member names, e.g. "FinanceView")
/// and the [Flags] bitmask stored on Role.
/// </summary>
/// <remarks>
/// Derived from the enum at startup, so new module bits are picked up automatically.
/// </remarks>
internal static class PermissionMapper
{
    // Every single-bit permission, excluding None, ordered by bit position.
    public static IReadOnlyList<Permission> Defined { get; } = Enum.GetValues<Permission>()
        .Where(p => p != Permission.None)
        .OrderBy(p => (long)p)
        .ToArray();

    public static Permission All => Defined.Aggregate(Permission.None, (acc, p) => acc | p);

    public static bool IsValidName(string name) =>
        name is not null
        && name != nameof(Permission.None)
        // Exact, case-sensitive match against a single member name, so "financeview", "16" and
        // "FinanceView, InventoryView" are rejected. Enum.TryParse would accept "16" and the
        // combined string (and lowercase with ignoreCase), which is why it isn't used here.
        && Enum.IsDefined(typeof(Permission), name);

    // Assumes names were already validated (see validators).
    public static Permission ToFlags(IEnumerable<string> names) =>
        names.Aggregate(Permission.None, (acc, name) => acc | Enum.Parse<Permission>(name));

    public static IReadOnlyList<string> ToNames(Permission flags) =>
        Defined.Where(p => flags.HasFlag(p)).Select(p => p.ToString()).ToList();
}
