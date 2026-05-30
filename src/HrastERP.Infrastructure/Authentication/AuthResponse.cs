namespace HrastERP.Infrastructure.Authentication;

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
