using FluentAssertions;
using HrastERP.SharedKernel.Results;

namespace HrastERP.SharedKernel.Tests.Results;

public class ResultTests
{
    private static readonly Error SomeError = Error.Validation("TEST", "Test error");

    // Non-generic Result

    [Fact]
    public void IsSuccess_is_true_When_Result_is_success()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void IsFailure_is_true_When_Result_is_failure()
    {
        var result = Result.Failure(SomeError);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Exposes_error_When_Result_is_failure()
    {
        var result = Result.Failure(SomeError);

        result.Error.Should().Be(SomeError);
    }

    [Fact]
    public void Has_None_error_When_Result_is_success()
    {
        var result = Result.Success();

        result.Error.Should().Be(Error.None);
    }

    // Generic Result<T>

    [Fact]
    public void IsSuccess_is_true_and_value_is_set_When_ResultT_succeeds()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void IsFailure_is_true_When_ResultT_is_failure()
    {
        var result = Result<int>.Failure(SomeError);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }

    [Fact]
    public void Throws_InvalidOperationException_When_accessing_Value_on_failure()
    {
        var result = Result<int>.Failure(SomeError);

        var act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Creates_success_When_implicitly_converted_from_value()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Creates_failure_When_implicitly_converted_from_error()
    {
        Result<string> result = SomeError;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }
}
