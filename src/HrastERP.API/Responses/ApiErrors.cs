using HrastERP.SharedKernel.Results;

namespace HrastERP.API.Responses;

/// <summary>
/// Errors specific to the API layer (controllers, authentication and authorization), raised before a request
/// reaches a handler. Generic ones (<c>General.Unauthorized</c>, <c>General.Forbidden</c>, ...) are in
/// <see cref="GeneralErrors"/>. They use the same codes and shapes as handler errors, so clients see one format for both.
/// </summary>
public static class ApiErrors
{
    /// <summary>
    /// The <c>{id}</c> in the URL differs from the <c>id</c> in the request body. Returned as a
    /// <c>General.Validation</c> error with a field error on <c>id</c>, like any other validation failure.
    /// Built on each access, so no response shares its field error dictionary with another.
    /// </summary>
    public static Error RouteIdMismatch => GeneralErrors.Validation(
        new Dictionary<string, string[]> { ["id"] = ["The id in the URL does not match the id in the body."] });

    /// <summary>
    /// The access token has expired; the client should get a new one from <c>POST /api/auth/refresh</c>.
    /// Written by <c>JwtChallengeResponse</c>.
    /// </summary>
    public static readonly Error TokenExpired = Error.Unauthorized(
        "Auth.TokenExpired",
        "The access token has expired.");

    /// <summary>
    /// The caller does not belong to the super admin tenant (<c>[RequireSuperAdminTenant]</c>).
    /// </summary>
    public static readonly Error SuperAdminTenantRequired = Error.Forbidden(
        "Tenant.SuperAdminRequired",
        "Only super admin tenant users can perform this operation.");
}
