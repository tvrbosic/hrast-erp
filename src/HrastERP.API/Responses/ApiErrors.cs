using HrastERP.SharedKernel.Results;

namespace HrastERP.API.Responses;

/// <summary>
/// Errors raised by the API layer itself (controllers and authorization), before a request reaches a handler. They use the same codes
/// and shapes as handler errors, so clients see one format for both.
/// </summary>
public static class ApiErrors
{
    /// <summary>
    /// The <c>{id}</c> in the URL differs from the <c>id</c> in the request body. Returned as a
    /// <c>General.Validation</c> error with a field error on <c>id</c>, like any other validation failure.
    /// </summary>
    public static readonly Error RouteIdMismatch = Error.Validation(
        "General.Validation",
        "One or more validation errors occurred.",
        new Dictionary<string, string[]> { ["id"] = ["The id in the URL does not match the id in the body."] });

    /// <summary>
    /// The caller is authenticated but a policy denied access, e.g. a missing permission.
    /// The default 403 body, written by <c>ForbiddenResponseHandler</c>.
    /// </summary>
    public static readonly Error Forbidden = Error.Forbidden(
        "General.Forbidden",
        "You do not have permission to perform this action.");

    /// <summary>
    /// The caller does not belong to the super admin tenant (<c>[RequireSuperAdminTenant]</c>).
    /// </summary>
    public static readonly Error SuperAdminTenantRequired = Error.Forbidden(
        "Tenant.SuperAdminRequired",
        "Only super admin tenant users can perform this operation.");
}
