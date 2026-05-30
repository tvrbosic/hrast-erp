using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentAssertions;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HrastERP.Infrastructure.Tests.Authentication;

public class TokenServiceTests
{
    private readonly JwtSettings _settings = new()
    {
        SecretKey = "ThisIsATestSecretKeyWithAtLeast32Characters!",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    private readonly ITokenService _sut;

    public TokenServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(_settings));
        services.AddScoped<ITokenService, TokenService>();
        var provider = services.BuildServiceProvider();
        _sut = provider.GetRequiredService<ITokenService>();
    }

    private static ApplicationUser CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        UserName = "test@example.com",
        TenantId = Guid.NewGuid(),
        FirstName = "John",
        LastName = "Doe"
    };

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        var user = CreateTestUser();

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == user.TenantId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.GivenName && c.Value == user.FirstName);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.FamilyName && c.Value == user.LastName);
    }

    [Fact]
    public void GenerateAccessToken_HasCorrectExpiry()
    {
        var user = CreateTestUser();
        var beforeGeneration = DateTime.UtcNow;

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expectedExpiry = beforeGeneration.AddMinutes(_settings.AccessTokenExpirationMinutes);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateAccessToken_IsSignedWithHmacSha256()
    {
        var user = CreateTestUser();

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void GenerateAccessToken_CanBeValidated()
    {
        var user = CreateTestUser();
        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_settings.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(token, validationParams, out _);

        principal.Should().NotBeNull();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokens()
    {
        var (raw1, _) = _sut.GenerateRefreshToken();
        var (raw2, _) = _sut.GenerateRefreshToken();

        raw1.Should().NotBe(raw2);
    }

    [Fact]
    public void GenerateRefreshToken_HashMatchesRawToken()
    {
        var (rawToken, hashedToken) = _sut.GenerateRefreshToken();

        var reHashed = _sut.HashToken(rawToken);

        reHashed.Should().Be(hashedToken);
    }

    [Fact]
    public void HashToken_IsDeterministic()
    {
        const string input = "test-token-value";

        var hash1 = _sut.HashToken(input);
        var hash2 = _sut.HashToken(input);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashToken_DifferentInputs_ProduceDifferentHashes()
    {
        var hash1 = _sut.HashToken("token-a");
        var hash2 = _sut.HashToken("token-b");

        hash1.Should().NotBe(hash2);
    }
}
