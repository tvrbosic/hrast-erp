using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Tenants.Models;
using HrastERP.Administration.Application.Tenants.Queries.GetTenantById;
using HrastERP.SharedKernel.Results;

namespace Administration.Tests.Tenants;

[Trait("Category", "Integration")]
public class GetTenantByIdQueryHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result<TenantResponse>> HandleAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await new GetTenantByIdQueryHandler(db).Handle(new GetTenantByIdQuery(id), CancellationToken.None);
    }

    [Fact]
    public async Task Returns_tenant_When_it_exists()
    {
        var tenant = await SeedTenantAsync("Acme");

        var result = await HandleAsync(tenant.Id);

        result.Value.Id.Should().Be(tenant.Id);
        result.Value.Name.Should().Be("Acme");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Returns_NotFound_When_tenant_does_not_exist()
    {
        var result = await HandleAsync(Guid.NewGuid());

        result.Error.Should().Be(AdministrationErrors.TenantNotFound);
    }
}
