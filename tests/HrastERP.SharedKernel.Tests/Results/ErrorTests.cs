using FluentAssertions;
using HrastERP.SharedKernel.Results;

namespace HrastERP.SharedKernel.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Has_empty_code_and_message_When_error_is_None()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }

    [Fact]
    public void Is_equal_When_code_and_message_match()
    {
        var a = Error.Validation("ERR001", "Something failed");
        var b = Error.Validation("ERR001", "Something failed");

        a.Should().Be(b);
    }

    [Fact]
    public void Is_not_equal_When_codes_differ()
    {
        var a = Error.Validation("ERR001", "Something failed");
        var b = Error.Validation("ERR002", "Something failed");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Sets_type_to_NotFound_When_created_via_NotFound_factory()
    {
        var error = Error.NotFound("User.NotFound", "User was not found");

        error.Code.Should().Be("User.NotFound");
        error.Message.Should().Be("User was not found");
        error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Sets_type_to_Validation_When_created_via_Validation_factory()
    {
        var error = Error.Validation("User.InvalidEmail", "Email is invalid");

        error.Code.Should().Be("User.InvalidEmail");
        error.Message.Should().Be("Email is invalid");
        error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Sets_type_to_Forbidden_When_created_via_Forbidden_factory()
    {
        var error = Error.Forbidden("User.Forbidden", "Access denied");

        error.Code.Should().Be("User.Forbidden");
        error.Message.Should().Be("Access denied");
        error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Sets_type_to_Conflict_When_created_via_Conflict_factory()
    {
        var error = Error.Conflict("User.Duplicate", "User already exists");

        error.Code.Should().Be("User.Duplicate");
        error.Message.Should().Be("User already exists");
        error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Sets_type_to_Unexpected_When_created_via_Unexpected_factory()
    {
        var error = Error.Unexpected("Server.Error", "Something went wrong");

        error.Code.Should().Be("Server.Error");
        error.Message.Should().Be("Something went wrong");
        error.Type.Should().Be(ErrorType.Unexpected);
    }
}
