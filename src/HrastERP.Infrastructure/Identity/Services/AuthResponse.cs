namespace HrastERP.Infrastructure.Identity;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    AuthenticatedUserResponse User);
