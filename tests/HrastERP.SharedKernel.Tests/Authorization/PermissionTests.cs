using FluentAssertions;
using HrastERP.SharedKernel.Authorization;

namespace HrastERP.SharedKernel.Tests.Authorization;

public class PermissionTests
{
    [Fact]
    public void None_HasValue_Zero()
    {
        ((long)Permission.None).Should().Be(0);
    }

    [Fact]
    public void None_ToString_ReturnsNone()
    {
        Permission.None.ToString().Should().Be("None");
    }

    [Fact]
    public void AllNonNoneFlags_HaveUniquePowerOfTwoValues()
    {
        var values = Enum.GetValues<Permission>()
            .Where(p => p != Permission.None)
            .Select(p => (long)p)
            .ToList();

        values.Should().OnlyHaveUniqueItems();
        values.Should().AllSatisfy(v => (v & (v - 1)).Should().Be(0)); // power of 2
    }

    [Fact]
    public void CombinedFlags_BitwiseAnd_DetectsSetPermission()
    {
        var combined = Permission.FinanceView | Permission.FinanceCreate;

        (combined & Permission.FinanceView).Should().NotBe(Permission.None);
        (combined & Permission.FinanceCreate).Should().NotBe(Permission.None);
    }

    [Fact]
    public void CombinedFlags_BitwiseAnd_RejectsUnsetPermission()
    {
        var combined = Permission.FinanceView | Permission.FinanceCreate;

        (combined & Permission.FinanceEdit).Should().Be(Permission.None);
    }

    [Fact]
    public void PermissionEnum_HasTwentyNonNoneValues()
    {
        var nonNone = Enum.GetValues<Permission>()
            .Where(p => p != Permission.None)
            .ToList();

        nonNone.Should().HaveCount(20);
    }

    [Fact]
    public void ToString_ReturnsReadableName()
    {
        Permission.FinanceView.ToString().Should().Be("FinanceView");
        Permission.ProcurementDelete.ToString().Should().Be("ProcurementDelete");
    }
}
