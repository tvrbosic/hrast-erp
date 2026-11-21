using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Tenants.Commands.ActivateTenant;
using HrastERP.Administration.Application.Tenants.Commands.DeactivateTenant;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Administration.Tests.Tenants;

[Trait("Category", "Integration")]
public class TenantStatusCommandHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    private async Task<Result> ActivateAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await new ActivateTenantCommandHandler(db, _cache)
            .Handle(new ActivateTenantCommand(id), CancellationToken.None);
    }

    private async Task<Result> DeactivateAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await new DeactivateTenantCommandHandler(db, _cache)
            .Handle(new DeactivateTenantCommand(id), CancellationToken.None);
    }

    private async Task<bool> IsActiveAsync(Guid id)
    {
        await using var db = NewDbContext();
        return (await db.Set<Tenant>().SingleAsync(t => t.Id == id)).IsActive;
    }

    [Fact]
    public async Task Returns_SuperAdminImmutable_When_deactivating_super_admin_tenant()
    {
        await SeedTenantAsync("Super Admin", TenantConstants.SuperAdminTenantId);

        var result = await DeactivateAsync(TenantConstants.SuperAdminTenantId);

        result.Error.Should().Be(AdministrationErrors.TenantSuperAdminImmutable);
        (await IsActiveAsync(TenantConstants.SuperAdminTenantId)).Should().BeTrue();
    }

    [Fact]
    public async Task Deactivates_tenant_and_clears_cached_status_When_tenant_exists()
    {
        var tenant = await SeedTenantAsync();

        var result = await DeactivateAsync(tenant.Id);

        result.IsSuccess.Should().BeTrue();
        (await IsActiveAsync(tenant.Id)).Should().BeFalse();
        await _cache.Received(1).RemoveAsync($"Tenants:active:{tenant.Id}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activates_tenant_and_clears_cached_status_When_tenant_exists()
    {
        var tenant = await SeedTenantAsync();
        await DeactivateAsync(tenant.Id);
        _cache.ClearReceivedCalls();

        var result = await ActivateAsync(tenant.Id);

        result.IsSuccess.Should().BeTrue();
        (await IsActiveAsync(tenant.Id)).Should().BeTrue();
        await _cache.Received(1).RemoveAsync($"Tenants:active:{tenant.Id}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_NotFound_When_deactivating_tenant_that_does_not_exist()
    {
        var result = await DeactivateAsync(Guid.NewGuid());

        result.Error.Should().Be(AdministrationErrors.TenantNotFound);
    }

    [Fact]
    public async Task Returns_NotFound_When_activating_tenant_that_does_not_exist()
    {
        var result = await ActivateAsync(Guid.NewGuid());

        result.Error.Should().Be(AdministrationErrors.TenantNotFound);
    }

    [Fact]
    public async Task Activates_super_admin_tenant_When_requested()
    {
        await SeedTenantAsync("Super Admin", TenantConstants.SuperAdminTenantId);

        var result = await ActivateAsync(TenantConstants.SuperAdminTenantId);

        result.IsSuccess.Should().BeTrue();
    }
}
