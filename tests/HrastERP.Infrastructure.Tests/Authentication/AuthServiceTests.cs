using FluentAssertions;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Configuration;
using HrastERP.Infrastructure.Persistence;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Authentication;

public class AuthServiceTests : IAsyncDisposable
{
    private sealed class FakeCurrentTenant : ICurrentTenant
    {
        public Guid TenantId => Guid.Empty;
    }

    private readonly JwtSettings _settings = new()
    {
        SecretKey = "ThisIsATestSecretKeyWithAtLeast32Characters!",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    private readonly HrastDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IAuthService _sut;
    private readonly ServiceProvider _serviceProvider;

    public AuthServiceTests()
    {
        var services = new ServiceCollection();

        var dbName = Guid.NewGuid().ToString();

        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        services.AddSingleton<IEnumerable<EntityConfigurationAssembly>>([]);

        services.AddDbContext<HrastDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<HrastDbContext>();

        services.AddSingleton(Options.Create(_settings));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<HrastDbContext>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _tokenService = _serviceProvider.GetRequiredService<ITokenService>();
        _sut = _serviceProvider.GetRequiredService<IAuthService>();
    }

    private async Task<ApplicationUser> SeedUserAsync(
        string email = "test@example.com",
        string password = "Password1",
        bool isActive = true)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            TenantId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            IsActive = isActive
        };

        var result = await _userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue();

        return user;
    }

    private async Task<RefreshToken> SeedRefreshTokenAsync(
        Guid userId, string hashedToken, int expiresInDays = 7, bool revoked = false)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = hashedToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays),
            RevokedAt = revoked ? DateTime.UtcNow : null
        };

        _dbContext.Set<RefreshToken>().Add(token);
        await _dbContext.SaveChangesAsync();

        return token;
    }

    // Login tests

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync("test@example.com", "Password1");

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_InvalidEmail_ReturnsInvalidCredentials()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync("wrong@example.com", "Password1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsInvalidCredentials()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync("test@example.com", "WrongPassword1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsInactiveUser()
    {
        await SeedUserAsync(isActive: false);

        var result = await _sut.LoginAsync("test@example.com", "Password1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InactiveUser);
    }

    [Fact]
    public async Task LoginAsync_PersistsRefreshTokenInDatabase()
    {
        await SeedUserAsync();

        await _sut.LoginAsync("test@example.com", "Password1");

        var tokens = await _dbContext.Set<RefreshToken>().ToListAsync();
        tokens.Should().HaveCount(1);
    }

    // Refresh tests

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewTokensAndRevokesOld()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken);

        var result = await _sut.RefreshAsync(rawToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBe(rawToken);

        var oldToken = await _dbContext.Set<RefreshToken>()
            .FirstAsync(rt => rt.Token == hashedToken);
        oldToken.IsRevoked.Should().BeTrue();
        oldToken.ReplacedByToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ReturnsError()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken, expiresInDays: -1);

        var result = await _sut.RefreshAsync(rawToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ReturnsError()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken, revoked: true);

        var result = await _sut.RefreshAsync(rawToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_NonexistentToken_ReturnsError()
    {
        var result = await _sut.RefreshAsync("nonexistent-token");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    // Logout tests

    [Fact]
    public async Task LogoutAsync_ValidToken_RevokesIt()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken);

        var result = await _sut.LogoutAsync(rawToken);

        result.IsSuccess.Should().BeTrue();
        var storedToken = await _dbContext.Set<RefreshToken>()
            .FirstAsync(rt => rt.Token == hashedToken);
        storedToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_AlreadyRevokedToken_ReturnsSuccess()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken, revoked: true);

        var result = await _sut.LogoutAsync(rawToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_NonexistentToken_ReturnsSuccess()
    {
        var result = await _sut.LogoutAsync("nonexistent-token");

        result.IsSuccess.Should().BeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }
}
