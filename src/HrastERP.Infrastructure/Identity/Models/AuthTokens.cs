namespace HrastERP.Infrastructure.Identity;

public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    AuthenticatedUser User);
