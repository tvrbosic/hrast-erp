namespace HrastERP.SharedKernel.Results;

/// <summary>
/// Category of an <see cref="Error"/>. The API maps it to the HTTP status code:
/// Validation 422, NotFound 404, Forbidden 403, Conflict 409, Unexpected 500.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Forbidden,
    Conflict,
    Unexpected
}
