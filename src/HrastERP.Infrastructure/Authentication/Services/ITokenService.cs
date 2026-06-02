namespace HrastERP.Infrastructure.Authentication;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user);
    (string rawToken, string hashedToken) GenerateRefreshToken();
    string HashToken(string rawToken);
}
