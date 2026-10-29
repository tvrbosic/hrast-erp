using FluentAssertions;
using HrastERP.Administration.Application.Roles;
using HrastERP.Administration.Application.Roles.Queries.GetPermissions;

namespace Administration.Tests.Roles;

public class GetPermissionsQueryHandlerTests
{
    private readonly GetPermissionsQueryHandler _sut = new();

    [Fact]
    public async Task Returns_five_module_groups_in_bit_order_When_handled()
    {
        var result = await _sut.Handle(new GetPermissionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(g => g.Module).Should()
            .Equal("Administration", "Finance", "Inventory", "Procurement", "Production");
    }

    [Fact]
    public async Task Returns_twenty_permissions_without_None_When_handled()
    {
        var result = await _sut.Handle(new GetPermissionsQuery(), CancellationToken.None);

        var all = result.Value.SelectMany(g => g.Permissions).ToList();
        all.Should().HaveCount(20);
        all.Should().NotContain("None");
    }

    [Fact]
    public async Task Orders_permissions_by_bit_within_group_When_handled()
    {
        var result = await _sut.Handle(new GetPermissionsQuery(), CancellationToken.None);

        result.Value.First().Permissions.Should()
            .Equal("AdministrationView", "AdministrationCreate", "AdministrationEdit", "AdministrationDelete");
    }
}

public class GetPermissionsQueryHandlerModuleNameTests
{
    [Theory]
    [InlineData("FinanceView", "Finance")]
    [InlineData("ProcurementDelete", "Procurement")]
    public void Strips_action_suffix_When_name_ends_with_known_action(string permission, string module)
    {
        GetPermissionsQueryHandler.ToModuleName(permission).Should().Be(module);
    }

    [Fact]
    public void Returns_full_name_When_name_has_no_known_action_suffix()
    {
        GetPermissionsQueryHandler.ToModuleName("FinanceApprove").Should().Be("FinanceApprove");
    }

    // Guard: a new permission with an unknown action still works (it gets its own group), but it
    // usually means ActionSuffixes needs the new action added so it groups under its module.
    [Fact]
    public void Every_defined_permission_ends_with_a_known_action_suffix_When_enum_is_inspected()
    {
        PermissionMapper.Defined.Select(p => p.ToString())
            .Should().OnlyContain(name => GetPermissionsQueryHandler.ActionSuffixes.Any(name.EndsWith));
    }
}
