namespace HrastERP.SharedKernel.Results;

/// <summary>
/// Represents a domain error returned by handlers instead of throwing exceptions.
/// Use the static factory methods to create typed errors (e.g. <see cref="NotFound"/>, <see cref="Validation"/>).
/// Modules should define their errors as static readonly constants in a dedicated <c>&lt;Module&gt;Errors</c> class.
/// </summary>
/// <param name="Code">Dot-separated identifier, e.g. <c>"Order.NotFound"</c>.</param>
/// <param name="Message">Human-readable description of the error.</param>
/// <param name="Type">Category used by the API layer to map to an HTTP status code.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>
    /// Sentinel value representing the absence of an error. Used internally by <see cref="Result"/>.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Validation);

    // Factory methods — each pre-fills the ErrorType so callers don't need to reference the enum directly.

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
