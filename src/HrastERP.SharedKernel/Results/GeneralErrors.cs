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
    /// The request reached a MediatR handler without a tenant context.
    /// </summary>
    public static readonly Error MissingTenant =
        Error.Forbidden("General.MissingTenant", "A valid tenant context is required.");

    /// <summary>
    /// The caller's user account or tenant was deactivated or deleted after the token was issued.
    /// </summary>
    public static readonly Error InactiveEntity =
        Error.Forbidden("General.InactiveEntity", "Your account or tenant has been deactivated. Contact your administrator.");

    /// <summary>
    /// An unhandled exception; the details are only logged, never returned.
    /// </summary>
    public static readonly Error Unexpected =
        Error.Unexpected("General.Unexpected", "An unexpected error occurred.");
}
