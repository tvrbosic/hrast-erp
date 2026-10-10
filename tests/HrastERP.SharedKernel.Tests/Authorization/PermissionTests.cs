using FluentAssertions;
using HrastERP.SharedKernel.Authorization;

namespace HrastERP.SharedKernel.Tests.Authorization;

public class PermissionTests
{
    [Fact]
    public void Has_value_zero_When_permission_is_None()
    {
        ((long)Permission.None).Should().Be(0);
    }

    [Fact]
    public void Returns_None_string_When_permission_is_None()
    {
        Permission.None.ToString().Should().Be("None");
    }

    [Fact]
    public void All_non_None_flags_are_unique_powers_of_two_When_enum_is_defined()
    {
        var values = Enum.GetValues<Permission>()
            .Where(p => p != Permission.None)
            .Select(p => (long)p)
            .ToList();

        values.Should().OnlyHaveUniqueItems();
        values.Should().AllSatisfy(v => (v & (v - 1)).Should().Be(0)); // power of 2
    }

    [Fact]
    public void Detects_set_permission_When_using_bitwise_and_on_combined_flags()
    {
        var combined = Permission.FinanceView | Permission.FinanceCreate;

        (combined & Permission.FinanceView).Should().NotBe(Permission.None);
        (combined & Permission.FinanceCreate).Should().NotBe(Permission.None);
    }

    [Fact]
    public void Returns_None_When_checking_unset_permission_via_bitwise_and()
    {
        var combined = Permission.FinanceView | Permission.FinanceCreate;

        (combined & Permission.FinanceEdit).Should().Be(Permission.None);
    }

    [Fact]
    public void Has_twenty_non_None_values_When_enum_is_defined()
    {
        var nonNone = Enum.GetValues<Permission>()
            .Where(p => p != Permission.None)
            .ToList();

        nonNone.Should().HaveCount(20);
    }

    [Fact]
    public void Returns_readable_name_When_converted_to_string()
    {
        Permission.FinanceView.ToString().Should().Be("FinanceView");
        Permission.ProcurementDelete.ToString().Should().Be("ProcurementDelete");
    }
}
