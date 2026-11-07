using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Authentication;

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

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);
        services.AddSingleton(currentTenant);

        // Module configurations must be applied so the model matches the migration snapshot
        // (e.g. ApplicationUser → Tenant FK from the Administration module)
        services.AddSingleton<IEnumerable<EntityConfigurationAssembly>>(
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)]);

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
}
