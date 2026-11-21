using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Identity;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Identity;

/// <summary>
/// Uses the production <c>AddIdentityServices()</c> registration, so a missing Identity
/// service (such as a token provider) fails here instead of as a 500 in the API.
/// </summary>
[Trait("Category", "Integration")]
public class IdentityServicesTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ServiceProvider _serviceProvider;
    private readonly HrastDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityServicesTests(PostgresContainerFixture fixture)
    {
        var services = new ServiceCollection();

        // UserManager's IdentityOptions are bound from the "Identity" section; "Password1" below is 9 characters
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:PasswordRequiredLength"] = "9" })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);
        services.AddSingleton(currentTenant);

        services.AddDbContext<HrastDbContext>(options =>
            options.UseNpgsql(fixture.NewConnectionString()));

        services.AddIdentityServices();
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<HrastDbContext>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.MigrateAsync();

        _dbContext.Set<Tenant>().Add(new Tenant(_tenantId, "Test Tenant"));
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Resets_password_with_generated_token_When_identity_uses_production_registration()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            UserName = "user@example.com",
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "User"
        };
        (await _userManager.CreateAsync(user, "Password1")).Succeeded.Should().BeTrue();

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, "NewPassword1");

        result.Succeeded.Should().BeTrue();
        (await _userManager.CheckPasswordAsync(user, "NewPassword1")).Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_password_shorter_than_configured_length_When_creating_user()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "short@example.com",
            UserName = "short@example.com",
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "User"
        };

        // 8 characters: meets every rule except the configured length of 9
        var result = await _userManager.CreateAsync(user, "Passwo1A");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "PasswordTooShort");
    }
}
