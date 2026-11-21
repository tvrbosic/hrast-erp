using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application.Tenants.Models;
using HrastERP.Administration.Application.Tenants.Queries.GetTenants;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Administration.Tests.Tenants;

[Trait("Category", "Integration")]
public class GetTenantsQueryHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result<PagedResult<TenantResponse>>> HandleAsync(int page, int pageSize, bool? isActive = null)
    {
        await using var db = NewDbContext();
        return await new GetTenantsQueryHandler(db)
            .Handle(new GetTenantsQuery(page, pageSize, isActive), CancellationToken.None);
    }

    [Fact]
    public async Task Returns_tenants_ordered_by_name_When_paged()
    {
        await SeedTenantAsync("Bravo");
        await SeedTenantAsync("Alpha");
        await SeedTenantAsync("Charlie");

        var result = await HandleAsync(page: 1, pageSize: 2);

        result.Value.Items.Select(t => t.Name).Should().Equal("Alpha", "Bravo");
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Returns_only_matching_tenants_When_filtered_by_is_active()
    {
        var inactive = await SeedTenantAsync("Inactive");
        await SeedTenantAsync("Active");
        await using (var db = NewDbContext())
        {
            var tenant = await db.Set<Tenant>().SingleAsync(t => t.Id == inactive.Id);
            tenant.Deactivate();
            await db.SaveChangesAsync();
        }

        var result = await HandleAsync(page: 1, pageSize: 10, isActive: false);

        result.Value.Items.Select(t => t.Name).Should().Equal("Inactive");
    }

    [Fact]
    public async Task Excludes_soft_deleted_tenants_When_listing()
    {
        var deleted = await SeedTenantAsync("Deleted");
        await SeedTenantAsync("Kept");
        await SoftDeleteTenantAsync(deleted.Id);

        var result = await HandleAsync(page: 1, pageSize: 10);

        result.Value.Items.Select(t => t.Name).Should().Equal("Kept");
        result.Value.TotalCount.Should().Be(1);
    }
}
