namespace HrastERP.SharedKernel.Constants;

public static class TenantConstants
{
    /// <summary>
    /// Seeded super admin tenant. Its users bypass tenant query filters and see every tenant's data;
    /// their role permissions are still enforced.
    /// </summary>
    public static readonly Guid SuperAdminTenantId = Guid.Parse("00000000-0000-0000-0000-100000000000");
}
