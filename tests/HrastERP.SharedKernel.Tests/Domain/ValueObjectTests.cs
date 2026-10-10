using FluentAssertions;
using HrastERP.SharedKernel.Domain;

namespace HrastERP.SharedKernel.Tests.Domain;

public class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }

    [Fact]
    public void Is_equal_When_components_are_identical()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        a.Should().Be(b);
    }

    [Fact]
    public void Is_not_equal_When_any_component_differs()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(20m, "EUR");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Has_equal_hash_codes_When_components_are_identical()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_operator_returns_true_When_components_are_identical()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_operator_returns_true_When_any_component_differs()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "USD");

        (a != b).Should().BeTrue();
    }
}
