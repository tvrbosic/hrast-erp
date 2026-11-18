using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Identity;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or has expired.");

    public static readonly Error InactiveUser =
        Error.Forbidden("Auth.InactiveUser", "This user account has been deactivated.");

    public static readonly Error InactiveTenant =
        Error.Forbidden("Auth.InactiveTenant", "This tenant has been deactivated.");

    public static readonly Error UserNotFound =
        Error.NotFound("Auth.UserNotFound", "The current user was not found.");
}
