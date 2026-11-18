using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace HrastERP.Administration.Application.Users;

// Turns Identity's errors into field errors keyed by camelCase request field, the same keys ValidationBehavior
// uses, so clients can show them next to the right input. Password rules (PasswordTooShort, PasswordRequiresDigit, ...)
// go under the command's password field, email and user name errors under "email" (the user name is the email).
// Errors that belong to no field (e.g. ConcurrencyFailure, InvalidToken) keep their camelCased Identity code.
internal static class IdentityFieldErrors
{
    public static IReadOnlyDictionary<string, string[]> From(IEnumerable<IdentityError> errors, string passwordProperty)
    {
        var passwordField = JsonNamingPolicy.CamelCase.ConvertName(passwordProperty);

        return errors
            .GroupBy(e => FieldFor(e.Code, passwordField))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
    }

    private static string FieldFor(string code, string passwordField) =>
        code.StartsWith("Password", StringComparison.Ordinal) ? passwordField
        : code.Contains("Email", StringComparison.Ordinal) || code.Contains("UserName", StringComparison.Ordinal) ? "email"
        : JsonNamingPolicy.CamelCase.ConvertName(code);
}
