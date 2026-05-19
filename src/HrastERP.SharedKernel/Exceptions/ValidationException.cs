namespace HrastERP.SharedKernel.Exceptions;

public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation failures occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
