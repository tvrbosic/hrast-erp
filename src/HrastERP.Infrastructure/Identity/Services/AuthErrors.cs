using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Identity;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Validation("Auth.InvalidCredentials", "Invalid email or password.");

    public static readonly Error InvalidRefreshToken =
        Error.Validation("Auth.InvalidRefreshToken", "The refresh token is invalid or has expired.");

    public static readonly Error InactiveUser =
        Error.Forbidden("Auth.InactiveUser", "This user account has been deactivated.");

    public static readonly Error UserNotFound =
        Error.NotFound("Auth.UserNotFound", "The current user was not found.");
}
