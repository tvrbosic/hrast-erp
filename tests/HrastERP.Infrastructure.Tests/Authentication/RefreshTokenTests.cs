using FluentAssertions;
using HrastERP.Infrastructure.Authentication;

namespace HrastERP.Infrastructure.Tests.Authentication;

public class RefreshTokenTests
{
    [Fact]
    public void IsExpired_is_true_When_ExpiresAt_is_in_the_past()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };

        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_is_false_When_ExpiresAt_is_in_the_future()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddMinutes(10) };

        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_is_true_When_RevokedAt_is_set()
    {
        var token = new RefreshToken { RevokedAt = DateTime.UtcNow };

        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_is_false_When_RevokedAt_is_null()
    {
        var token = new RefreshToken { RevokedAt = null };

        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IsActive_is_true_When_not_expired_and_not_revoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = null
        };

        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_is_false_When_token_is_expired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            RevokedAt = null
        };

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_is_false_When_token_is_revoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow
        };

        token.IsActive.Should().BeFalse();
    }
}
