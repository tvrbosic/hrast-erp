using System.Text.Json;
using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Database.Audit;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Authentication;

[Trait("Category", "Integration")]
public class ApplicationUserAuditTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private static readonly string[] IdentitySecretColumns = ["password_hash", "security_stamp", "concurrency_stamp"];

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ServiceProvider _serviceProvider;
    private readonly HrastDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationUserAuditTests(PostgresContainerFixture fixture)
    {
        var services = new ServiceCollection();

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns(_currentUserId);
        currentUser.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        services.AddSingleton(currentTenant);
        services.AddSingleton(currentUser);
        // Module configurations must be applied so the model matches the migration snapshot
        // (e.g. ApplicationUser → Tenant FK from the Administration module)
        services.AddSingleton<IEnumerable<EntityConfigurationAssembly>>(
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)]);

        string connectionString = fixture.NewConnectionString();
        services.AddDbContext<HrastDbContext>(options => options
            .UseNpgsql(connectionString)
            .AddInterceptors(
                new AuditableEntityInterceptor(currentUser),
                new SoftDeleteInterceptor(currentUser),
                new TenantEntityInterceptor(currentTenant),
                new AuditLogInterceptor(currentUser, currentTenant)));

        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<HrastDbContext>()
            .AddDefaultTokenProviders();

        // Password reset tokens need a data protection provider; the app itself registers no token providers.
        services.AddDataProtection();
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<HrastDbContext>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.MigrateAsync();

        // Users reference this tenant via the ApplicationUser → Tenant FK
        _dbContext.Set<Tenant>().Add(new Tenant(_tenantId, "Test Tenant"));
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    private async Task<ApplicationUser> CreateUserAsync()
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

        var result = await _userManager.CreateAsync(user, "Password1!");
        result.Succeeded.Should().BeTrue();

        return user;
    }

    private Task<List<AuditLogEntry>> GetUserAuditLogsAsync(Guid userId) =>
        _dbContext.Set<AuditLogEntry>()
            .IgnoreQueryFilters()
            .Where(e => e.EntityName == nameof(ApplicationUser) && e.EntityId == userId.ToString())
            .ToListAsync();

    private static IEnumerable<string> JsonKeys(string? json) =>
        json is null
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!.Keys;

    [Fact]
    public async Task Fills_audit_fields_When_user_is_created_and_updated()
    {
        var user = await CreateUserAsync();

        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        user.CreatedBy.Should().Be(_currentUserId);
        user.UpdatedAt.Should().BeNull();

        user.FirstName = "Changed";
        var result = await _userManager.UpdateAsync(user);
        result.Succeeded.Should().BeTrue();

        user.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        user.UpdatedBy.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Excludes_identity_secrets_from_audit_log_When_user_is_created()
    {
        var user = await CreateUserAsync();

        var logs = await GetUserAuditLogsAsync(user.Id);
        var created = logs.Should().ContainSingle(e => e.Action == AuditAction.Created).Subject;

        var keys = JsonKeys(created.NewValues).ToList();
        keys.Should().Contain(["email", "first_name"]);
        keys.Should().NotContain(IdentitySecretColumns);
    }

    [Fact]
    public async Task Records_users_own_tenant_When_super_admin_creates_and_deletes_user_in_another_tenant()
    {
        var user = await CreateUserAsync();

        _dbContext.Remove(user);
        await _dbContext.SaveChangesAsync();

        var logs = await GetUserAuditLogsAsync(user.Id);
        logs.Should().HaveCount(2).And.OnlyContain(e => e.TenantId == _tenantId);
    }

    [Fact]
    public async Task Excludes_identity_secrets_from_audit_log_When_password_is_changed()
    {
        var user = await CreateUserAsync();

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, "NewPassword1!");
        result.Succeeded.Should().BeTrue();

        var logs = await GetUserAuditLogsAsync(user.Id);
        logs.Should().Contain(e => e.Action == AuditAction.Updated);
        foreach (var log in logs)
        {
            JsonKeys(log.OldValues).Should().NotContain(IdentitySecretColumns);
            JsonKeys(log.NewValues).Should().NotContain(IdentitySecretColumns);
        }
    }

    [Fact]
    public async Task Hides_user_from_queries_and_UserManager_When_soft_deleted()
    {
        var user = await CreateUserAsync();

        _dbContext.Remove(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // AsNoTracking: a tracked instance would let FindByIdAsync answer from the change tracker
        var stored = await _dbContext.Set<ApplicationUser>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == user.Id);
        stored.Should().NotBeNull();
        stored!.DeletedAt.Should().NotBeNull();
        stored.DeletedBy.Should().Be(_currentUserId);

        (await _dbContext.Set<ApplicationUser>().AnyAsync(u => u.Id == user.Id)).Should().BeFalse();
        (await _userManager.FindByIdAsync(user.Id.ToString())).Should().BeNull();
    }
}
