using FluentAssertions;
using HrastERP.Administration.Application.Roles;
using HrastERP.SharedKernel.Authorization;

namespace Administration.Tests.Roles;

public class PermissionMapperTests
{
    [Theory]
    [InlineData("AdministrationView", true)]
    [InlineData("ProductionDelete", true)]
    [InlineData("administrationview", false)]
    [InlineData("NotAPermission", false)]
    [InlineData("", false)]
    [InlineData("AdministrationView, FinanceView", false)]
    public void Returns_expected_validity_When_checking_name(string name, bool expected)
    {
        PermissionMapper.IsValidName(name).Should().Be(expected);
    }

    [Fact]
    public void Returns_None_When_names_are_empty()
    {
        PermissionMapper.ToFlags([]).Should().Be(Permission.None);
    }

    [Fact]
    public void Ignores_duplicates_When_converting_to_flags()
    {
        var flags = PermissionMapper.ToFlags(["FinanceView", "FinanceView"]);

        flags.Should().Be(Permission.FinanceView);
    }

    [Fact]
    public void Returns_names_ordered_by_bit_When_converting_flags()
    {
        var names = PermissionMapper.ToNames(Permission.ProductionView | Permission.AdministrationEdit | Permission.FinanceView);

        names.Should().Equal(["ProductionView", "FinanceView", "AdministrationEdit", "FinanceView"]);
    }

    [Fact]
    public void Returns_empty_list_When_flags_are_None()
    {
        PermissionMapper.ToNames(Permission.None).Should().BeEmpty();
    }

    [Fact]
    public void Round_trips_names_When_converting_both_ways()
    {
        string[] names = ["AdministrationView", "InventoryCreate", "ProcurementDelete"];

        PermissionMapper.ToNames(PermissionMapper.ToFlags(names)).Should().Equal(names);
    }

    [Fact]
    public void Contains_every_defined_flag_When_using_All()
    {
        foreach (var permission in Enum.GetValues<Permission>())
        {
            PermissionMapper.All.HasFlag(permission).Should().BeTrue();
        }

        // Hardcoded on purpose: forces a conscious update when permissions are added,
        // and catches combined aliases (e.g. FinanceAll) sneaking into Defined.
        PermissionMapper.ToNames(PermissionMapper.All).Should().HaveCount(20);
    }
}
