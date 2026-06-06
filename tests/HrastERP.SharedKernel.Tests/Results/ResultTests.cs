using FluentAssertions;
using HrastERP.SharedKernel.Results;

namespace HrastERP.SharedKernel.Tests.Results;

public class ResultTests
{
    private static readonly Error SomeError = Error.Validation("TEST", "Test error");

    // Non-generic Result

    [Fact]
    public void Result_Success_is_success()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Result_Failure_is_failure()
    {
        var result = Result.Failure(SomeError);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Result_Failure_exposes_error()
    {
        var result = Result.Failure(SomeError);

        result.Error.Should().Be(SomeError);
    }

    [Fact]
    public void Result_Success_has_None_error()
    {
        var result = Result.Success();

        result.Error.Should().Be(Error.None);
    }

    // Generic Result<T>

    [Fact]
    public void ResultT_Success_is_success_with_value()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ResultT_Failure_is_failure()
    {
        var result = Result<int>.Failure(SomeError);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }

    [Fact]
    public void ResultT_Value_throws_when_failure()
    {
        var result = Result<int>.Failure(SomeError);

        var act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResultT_implicit_conversion_from_value_creates_success()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ResultT_implicit_conversion_from_error_creates_failure()
    {
        Result<string> result = SomeError;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SomeError);
    }
}
