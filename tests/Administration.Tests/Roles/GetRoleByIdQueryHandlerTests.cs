using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Roles.Queries.GetRoleById;
using HrastERP.SharedKernel.Authorization;

namespace Administration.Tests.Roles;

[Trait("Category", "Integration")]
public class GetRoleByIdQueryHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task Returns_role_When_role_exists()
    {
        var role = await SeedRoleAsync("Clerk", Permission.InventoryView);
        await using var db = NewDbContext();

        var result = await new GetRoleByIdQueryHandler(db).Handle(new GetRoleByIdQuery(role.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Clerk");
        result.Value.Permissions.Should().Equal("InventoryView");
    }

    [Fact]
    public async Task Returns_NotFound_When_role_does_not_exist()
    {
        await using var db = NewDbContext();

        var result = await new GetRoleByIdQueryHandler(db).Handle(new GetRoleByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.Should().Be(AdministrationErrors.RoleNotFound);
    }
}
