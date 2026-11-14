namespace HrastERP.Infrastructure.Database;

/// <summary>
/// Database schema names for every entity configuration, in modules and in Infrastructure.
/// A table's schema is the module whose business data it holds; technical tables no module owns go in
/// <see cref="Infrastructure"/>. Users and roles are configured in Infrastructure (auth needs them and
/// Infrastructure cannot reference a module) but their data belongs to Administration, so they use
/// <see cref="Administration"/>.
/// </summary>
public static class DatabaseSchemas
{
    public const string Administration = "administration";
    public const string Finance = "finance";
    public const string Inventory = "inventory";
    public const string Procurement = "procurement";
    public const string Production = "production";

    public const string Infrastructure = "infrastructure";
    public const string Hangfire = "hangfire";
}
