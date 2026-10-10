using FluentAssertions;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Authentication;

[Trait("Category", "Integration")]
public class AuthServiceTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
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

    public AuthServiceTests(PostgresContainerFixture fixture)
    {
        var services = new ServiceCollection();

        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        services.AddSingleton<ICurrentTenant>(tenant);
        services.AddSingleton<IEnumerable<EntityConfigurationAssembly>>([]);

        services.AddDbContext<HrastDbContext>(options =>
            options.UseNpgsql(fixture.NewConnectionString()));

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

    public async Task InitializeAsync()
    {
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
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
    public async Task Returns_success_When_credentials_are_valid()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync("test@example.com", "Password1");

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Returns_invalid_credentials_When_email_is_not_found()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync("wrong@example.com", "Password1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Returns_invalid_credentials_When_password_is_wrong()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync("test@example.com", "WrongPassword1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Returns_inactive_user_error_When_user_is_inactive()
    {
        await SeedUserAsync(isActive: false);

        var result = await _sut.LoginAsync("test@example.com", "Password1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InactiveUser);
    }

    [Fact]
    public async Task Persists_refresh_token_to_database_When_login_succeeds()
    {
        await SeedUserAsync();

        await _sut.LoginAsync("test@example.com", "Password1");

        var tokens = await _dbContext.Set<RefreshToken>().ToListAsync();
        tokens.Should().HaveCount(1);
    }

    // Refresh tests

    [Fact]
    public async Task Returns_new_tokens_and_revokes_old_token_When_refresh_token_is_valid()
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
    public async Task Returns_error_When_refresh_token_is_expired()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken, expiresInDays: -1);

        var result = await _sut.RefreshAsync(rawToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Returns_error_When_refresh_token_is_revoked()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken, revoked: true);

        var result = await _sut.RefreshAsync(rawToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Returns_error_When_refresh_token_does_not_exist()
    {
        var result = await _sut.RefreshAsync("nonexistent-token");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    // Logout tests

    [Fact]
    public async Task Revokes_token_When_logout_is_called_with_valid_token()
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
    public async Task Returns_success_When_token_was_already_revoked()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken, revoked: true);

        var result = await _sut.LogoutAsync(rawToken);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Returns_success_When_token_does_not_exist()
    {
        var result = await _sut.LogoutAsync("nonexistent-token");

        result.IsSuccess.Should().BeTrue();
    }

}
