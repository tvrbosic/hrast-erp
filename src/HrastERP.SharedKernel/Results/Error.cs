namespace HrastERP.SharedKernel.Results;

/// <summary>
/// Represents a domain error returned by handlers instead of throwing exceptions.
/// Use the static factory methods to create typed errors (e.g. <see cref="NotFound"/>, <see cref="Validation"/>).
/// Modules should define their errors as static readonly constants in a dedicated <c>&lt;Module&gt;Errors</c> class.
/// </summary>
/// <param name="Code">Dot-separated identifier, e.g. <c>"Order.NotFound"</c>.</param>
/// <param name="Message">Human-readable description of the error.</param>
/// <param name="Type">Category used by the API layer to map to an HTTP status code.</param>
/// <remarks>
/// Validation errors may carry optional field-level detail via <see cref="ValidationErrors"/>
/// (e.g. <c>{ "quantity": ["Must be greater than zero."] }</c>). Pass it through the
/// <see cref="Validation"/> factory — it is not a constructor parameter because it is optional
/// metadata that only applies to one error type.
/// </remarks>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    // Optional metadata for validation errors only. Declared as init (not a constructor parameter)
    // to keep it out of the core identity fields and avoid requiring a 4th argument on every constructor call.
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    /// <summary>
    /// Sentinel value representing the absence of an error. Used internally by <see cref="Result"/>.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Validation);

    // Factory methods — each pre-fills the ErrorType so callers don't need to reference the enum directly.
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Validation(string code, string message,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        => new(code, message, ErrorType.Validation) { ValidationErrors = validationErrors };

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
