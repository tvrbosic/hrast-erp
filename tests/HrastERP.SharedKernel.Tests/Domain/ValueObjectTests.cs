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
    public void Value_objects_with_same_components_should_be_equal()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        a.Should().Be(b);
    }

    [Fact]
    public void Value_objects_with_different_components_should_not_be_equal()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(20m, "EUR");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Value_objects_with_same_components_have_equal_hash_codes()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_operator_returns_true_for_equal_value_objects()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_operator_returns_true_for_different_value_objects()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "USD");

        (a != b).Should().BeTrue();
    }
}
