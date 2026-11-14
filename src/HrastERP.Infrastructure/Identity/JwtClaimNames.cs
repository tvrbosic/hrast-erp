namespace HrastERP.Infrastructure.Identity;

/// <summary>
/// Names of the custom claims <c>TokenService</c> writes into the access token. Shared with the API's claim
/// readers (<c>CurrentUser</c>, <c>CurrentTenant</c>, <c>BlockInactiveEntityMiddleware</c>), so a typo on
/// either side can't silently turn into <c>Guid.Empty</c> or <c>Permission.None</c>.
/// </summary>
public static class JwtClaimNames
{
    /// <summary>The user's tenant id.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>The role's <c>Permission</c> flags as a decimal <c>long</c>.</summary>
    public const string Permissions = "permissions";
}
