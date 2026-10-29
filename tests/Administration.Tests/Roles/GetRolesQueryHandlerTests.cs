using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application.Roles.Queries.GetRoles;
using HrastERP.SharedKernel.Authorization;

namespace Administration.Tests.Roles;

[Trait("Category", "Integration")]
public class GetRolesQueryHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task Returns_requested_page_ordered_by_name_When_roles_exist()
    {
        await SeedRoleAsync("Charlie");
        await SeedRoleAsync("Alpha");
        await SeedRoleAsync("Bravo");
        await using var db = NewDbContext();

        var result = await new GetRolesQueryHandler(db).Handle(new GetRolesQuery(1, 2), CancellationToken.None);

        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Select(r => r.Name).Should().Equal("Alpha", "Bravo");
    }

    [Fact]
    public async Task Maps_permission_names_When_role_has_flags()
    {
        await SeedRoleAsync("Clerk", Permission.InventoryView | Permission.FinanceView);
        await using var db = NewDbContext();

        var result = await new GetRolesQueryHandler(db).Handle(new GetRolesQuery(1, 10), CancellationToken.None);

        result.Value.Items.Single().Permissions.Should().Equal("FinanceView", "InventoryView");
    }
}
