using HrastERP.Administration;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Administration.Tests.Fixtures;

// Real HrastDbContext against a per-class Postgres database. Interceptors are not registered,
// so audit/tenant fields must be set explicitly when seeding.
public abstract class IntegrationTestBase(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private readonly string _connectionString = fixture.NewConnectionString();

    // Defaults to the super admin tenant context — matches who is allowed to call role write endpoints.
    // Interceptors let a test inject a "concurrent" write right before SaveChanges to simulate races.
    protected HrastDbContext NewDbContext(Guid? tenantId = null, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<HrastDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(interceptors)
            .Options;

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(tenantId ?? TenantConstants.SuperAdminTenantId);

        return new HrastDbContext(
            options,
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)],
            currentTenant);
    }

    public async Task InitializeAsync()
    {
        await using var db = NewDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<Role> SeedRoleAsync(
        string name, Permission permissions = Permission.None, Guid? id = null)
    {
        var role = new Role
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            NormalizedName = name.Trim().ToUpperInvariant(),
            Description = $"{name} description",
            Permissions = permissions
        };

        await using var db = NewDbContext();
        db.Set<Role>().Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    protected async Task<Tenant> SeedTenantAsync(string name = "Acme", Guid? id = null)
    {
        var tenant = new Tenant(id ?? Guid.NewGuid(), name);

        await using var db = NewDbContext();
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    // Interceptors aren't registered here, so a Remove() would hard-delete; set deleted_at directly instead.
    protected async Task SoftDeleteTenantAsync(Guid tenantId)
    {
        await using var db = NewDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE administration.tenants SET deleted_at = now() WHERE id = {tenantId}");
    }

    protected async Task<ApplicationUser> SeedUserAsync(Guid tenantId, Guid? roleId, bool isActive = true)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User",
            TenantId = tenantId,
            RoleId = roleId,
            IsActive = isActive
        };

        await using var db = NewDbContext();
        db.Set<ApplicationUser>().Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}

// Runs an action once, right before the context it's attached to saves — i.e. after the handler's
// own pre-checks passed. Used to reproduce check-then-act races deterministically.
public sealed class BeforeSaveInterceptor(Func<Task> action) : SaveChangesInterceptor
{
    private bool _ran;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!_ran)
        {
            _ran = true;
            await action();
        }

        return result;
    }
}
