namespace HrastERP.SharedKernel.Results;

/// <summary>
/// The cross-cutting <c>General.*</c> errors, raised by pipeline behaviors, middleware and the API layer rather
/// than by a module. Module handlers return their own module's catalog (e.g. <c>AdministrationErrors</c>), so the
/// code names the actual failure.
/// </summary>
public static class GeneralErrors
{
    /// <summary>
    /// Input failed validation; <paramref name="fieldErrors"/> is keyed by camelCase field name.
    /// </summary>
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        Error.Validation("General.Validation", "One or more validation errors occurred.", fieldErrors);

    /// <summary>
    /// The request has no access token, or one that is invalid (bad signature, issuer or audience).
    /// </summary>
    public static readonly Error Unauthorized =
        Error.Unauthorized("General.Unauthorized", "A valid access token is required.");

    /// <summary>
    /// The caller is authenticated but a policy denied access, e.g. a missing permission.
    /// </summary>
    public static readonly Error Forbidden =
        Error.Forbidden("General.Forbidden", "You do not have permission to perform this action.");

    /// <summary>
    /// The request reached a MediatR handler without a tenant context, i.e. its token has no valid tenant claim.
    /// An authentication failure (401), since only a malformed token can lack it.
    /// </summary>
    public static readonly Error MissingTenant =
        Error.Unauthorized("General.MissingTenant", "A valid tenant context is required.");

    /// <summary>
    /// No endpoint matches the URL (404 written by <c>StatusCodeErrorResponse</c>). Not the same as a handler's
    /// <c>*.NotFound</c>, which means the URL exists but the entity doesn't.
    /// </summary>
    public static readonly Error RouteNotFound =
        Error.NotFound("General.RouteNotFound", "The requested URL does not exist.");

    /// <summary>
    /// The URL exists but not for this HTTP method (405 written by <c>StatusCodeErrorResponse</c>; the
    /// <c>Allow</c> header lists the supported methods).
    /// </summary>
    public static readonly Error MethodNotAllowed =
        Error.Validation("General.MethodNotAllowed", "The HTTP method is not allowed for this URL.");

    /// <summary>
    /// The request body's <c>Content-Type</c> can't be read (415 written by <c>StatusCodeErrorResponse</c>).
    /// </summary>
    public static readonly Error UnsupportedMediaType =
        Error.Validation("General.UnsupportedMediaType", "The request body must be JSON (Content-Type: application/json).");

    /// <summary>
    /// An unhandled exception; the details are only logged, never returned.
    /// </summary>
    public static readonly Error Unexpected =
        Error.Unexpected("General.Unexpected", "An unexpected error occurred.");
}
