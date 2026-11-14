using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Identity;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Identity;

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

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly HrastDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IAuthService _sut;
    private readonly ServiceProvider _serviceProvider;

    public AuthServiceTests(PostgresContainerFixture fixture)
    {
        var services = new ServiceCollection();

        _currentTenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        services.AddSingleton(_currentTenant);
        services.AddSingleton(_currentUser);

        // One database per test; captured once so contexts from extra scopes use the same database
        var connectionString = fixture.NewConnectionString();
        services.AddDbContext<HrastDbContext>(options => options.UseNpgsql(connectionString));

        services.AddIdentityServices();

        // Registered after AddIdentityServices() so it replaces the appsettings binding.
        services.AddSingleton(Options.Create(_settings));

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

        // Seeded users reference this tenant via the ApplicationUser → Tenant FK
        _dbContext.Set<Tenant>().Add(new Tenant(_tenantId, "Test Tenant"));
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    private async Task<ApplicationUser> SeedUserAsync(
        string email = "test@example.com",
        string password = "Password1",
        bool isActive = true,
        Guid? roleId = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "User",
            IsActive = isActive,
            RoleId = roleId
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

    private async Task<Role> SeedRoleAsync(Permission permissions)
    {
        var name = $"Role {Guid.NewGuid():N}";
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Permissions = permissions
        };

        _dbContext.Set<Role>().Add(role);
        await _dbContext.SaveChangesAsync();

        return role;
    }

    private async Task ChangeRolePermissionsAsync(Role role, Permission permissions)
    {
        role.Permissions = permissions;
        await _dbContext.SaveChangesAsync();
    }

    // Reads the permissions claim the way CurrentUser does and converts it to names
    private static IReadOnlyList<string> TokenPermissionNames(string accessToken)
    {
        var claim = new JwtSecurityTokenHandler().ReadJwtToken(accessToken)
            .Claims.Single(c => c.Type == "permissions").Value;
        return PermissionMapper.ToNames((Permission)long.Parse(claim, CultureInfo.InvariantCulture));
    }

    // Authenticated user tests

    [Fact]
    public async Task Returns_authenticated_user_with_tenant_and_role_When_login_succeeds()
    {
        // Arrange: a user with a role holding two permissions
        var role = await SeedRoleAsync(Permission.FinanceView | Permission.AdministrationView);
        var user = await SeedUserAsync(roleId: role.Id);

        // Act
        var result = await _sut.LoginAsync("test@example.com", "Password1");

        // Assert: user, tenant and role fields, with permission names in bit order
        result.IsSuccess.Should().BeTrue();
        result.Value.User.Should().BeEquivalentTo(new AuthenticatedUser(
            user.Id,
            "test@example.com",
            "Test",
            "User",
            _tenantId,
            "Test Tenant",
            role.Id,
            role.Name,
            PermissionMapper.ToNames(Permission.AdministrationView | Permission.FinanceView)));
        result.Value.User.Permissions.Should().Equal("AdministrationView", "FinanceView");
    }

    [Fact]
    public async Task Returns_permission_names_matching_token_When_login_succeeds()
    {
        var role = await SeedRoleAsync(Permission.InventoryView | Permission.InventoryEdit | Permission.ProductionView);
        await SeedUserAsync(roleId: role.Id);

        var result = await _sut.LoginAsync("test@example.com", "Password1");

        result.Value.User.Permissions.Should().NotBeEmpty()
            .And.Equal(TokenPermissionNames(result.Value.AccessToken));
    }

    [Fact]
    public async Task Returns_empty_permissions_and_null_role_When_user_has_no_role()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync("test@example.com", "Password1");

        result.Value.User.RoleId.Should().BeNull();
        result.Value.User.RoleName.Should().BeNull();
        result.Value.User.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_tenant_name_When_login_runs_outside_users_tenant()
    {
        await SeedUserAsync();
        // A regular tenant context other than the user's, so tenant query filters would hide the user.
        // HrastDbContext captures the tenant when it is created, so resolve the service from a new scope.
        _currentTenant.TenantId.Returns(Guid.NewGuid());
        using var scope = _serviceProvider.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await sut.LoginAsync("test@example.com", "Password1");

        result.Value.User.TenantName.Should().Be("Test Tenant");
    }

    [Fact]
    public async Task Returns_authenticated_user_When_refresh_succeeds()
    {
        var role = await SeedRoleAsync(Permission.FinanceView);
        await SeedUserAsync(roleId: role.Id);
        var login = await _sut.LoginAsync("test@example.com", "Password1");

        var result = await _sut.RefreshAsync(login.Value.RefreshToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Should().BeEquivalentTo(login.Value.User);
    }

    [Fact]
    public async Task Returns_empty_permissions_and_null_role_When_refreshing_user_without_role()
    {
        await SeedUserAsync();
        var login = await _sut.LoginAsync("test@example.com", "Password1");

        var result = await _sut.RefreshAsync(login.Value.RefreshToken);

        result.Value.User.RoleId.Should().BeNull();
        result.Value.User.RoleName.Should().BeNull();
        result.Value.User.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_new_role_permissions_When_refresh_follows_role_change()
    {
        var role = await SeedRoleAsync(Permission.FinanceView);
        await SeedUserAsync(roleId: role.Id);
        var login = await _sut.LoginAsync("test@example.com", "Password1");
        await ChangeRolePermissionsAsync(role, Permission.InventoryView);

        var result = await _sut.RefreshAsync(login.Value.RefreshToken);

        result.Value.User.Permissions.Should().Equal("InventoryView")
            .And.Equal(TokenPermissionNames(result.Value.AccessToken));
    }

    // Current user (/me) tests

    [Fact]
    public async Task Returns_token_permissions_not_role_permissions_When_role_changed_after_login()
    {
        // Arrange: a logged-in user whose token carries FinanceView, then the role changes
        var role = await SeedRoleAsync(Permission.FinanceView);
        var user = await SeedUserAsync(roleId: role.Id);
        _currentUser.UserId.Returns(user.Id);
        _currentUser.EffectivePermissions.Returns(Permission.FinanceView);
        role.Name = $"Renamed {Guid.NewGuid():N}";
        role.NormalizedName = role.Name.ToUpperInvariant();
        await ChangeRolePermissionsAsync(role, Permission.InventoryView);

        // Act
        var result = await _sut.GetCurrentUserAsync();

        // Assert: permissions come from the token, everything else from the database
        result.IsSuccess.Should().BeTrue();
        result.Value.Permissions.Should().Equal("FinanceView");
        result.Value.RoleName.Should().Be(role.Name);
        result.Value.TenantName.Should().Be("Test Tenant");
    }

    [Fact]
    public async Task Returns_current_user_When_user_is_soft_deleted()
    {
        var user = await SeedUserAsync();
        // /me adds no checks of its own (the middleware blocks deleted users), so it must not 404 or throw
        await _dbContext.Database.ExecuteSqlAsync(
            $"UPDATE administration.users SET deleted_at = now() WHERE id = {user.Id}");
        _currentUser.UserId.Returns(user.Id);

        var result = await _sut.GetCurrentUserAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task Returns_UserNotFound_When_current_user_does_not_exist()
    {
        _currentUser.UserId.Returns(Guid.NewGuid());

        var result = await _sut.GetCurrentUserAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
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
    public async Task Returns_inactive_user_error_When_logging_in_as_deleted_user_still_marked_active()
    {
        var user = await SeedUserAsync();
        // Soft-delete only; is_active stays true so the DeletedAt check alone must reject the login
        await _dbContext.Database.ExecuteSqlAsync(
            $"UPDATE administration.users SET deleted_at = now() WHERE id = {user.Id}");
        // Raw SQL bypasses the change tracker, which would otherwise return the stale active user
        _dbContext.ChangeTracker.Clear();

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
    public async Task Returns_inactive_user_error_When_refreshing_token_of_inactive_user()
    {
        var user = await SeedUserAsync(isActive: false);
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        var seeded = await SeedRefreshTokenAsync(user.Id, hashedToken);

        var result = await _sut.RefreshAsync(rawToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InactiveUser);

        var tokens = await _dbContext.Set<RefreshToken>().Where(rt => rt.UserId == user.Id).ToListAsync();
        tokens.Should().ContainSingle();
        tokens[0].Id.Should().Be(seeded.Id);
        tokens[0].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Returns_inactive_user_error_When_refreshing_token_of_deleted_user_still_marked_active()
    {
        var user = await SeedUserAsync();
        var (rawToken, hashedToken) = _tokenService.GenerateRefreshToken();
        await SeedRefreshTokenAsync(user.Id, hashedToken);
        await _dbContext.Database.ExecuteSqlAsync(
            $"UPDATE administration.users SET deleted_at = now() WHERE id = {user.Id}");
        _dbContext.ChangeTracker.Clear();

        var result = await _sut.RefreshAsync(rawToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InactiveUser);
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
