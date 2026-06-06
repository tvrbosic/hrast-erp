using FluentAssertions;
using HrastERP.SharedKernel.Results;

namespace HrastERP.SharedKernel.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Error_None_has_empty_code_and_message()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }

    [Fact]
    public void Errors_with_same_code_and_message_are_equal()
    {
        var a = Error.Validation("ERR001", "Something failed");
        var b = Error.Validation("ERR001", "Something failed");

        a.Should().Be(b);
    }

    [Fact]
    public void Errors_with_different_codes_are_not_equal()
    {
        var a = Error.Validation("ERR001", "Something failed");
        var b = Error.Validation("ERR002", "Something failed");

        a.Should().NotBe(b);
    }

    [Fact]
    public void NotFound_factory_sets_type_to_NotFound()
    {
        var error = Error.NotFound("User.NotFound", "User was not found");

        error.Code.Should().Be("User.NotFound");
        error.Message.Should().Be("User was not found");
        error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validation_factory_sets_type_to_Validation()
    {
        var error = Error.Validation("User.InvalidEmail", "Email is invalid");

        error.Code.Should().Be("User.InvalidEmail");
        error.Message.Should().Be("Email is invalid");
        error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Forbidden_factory_sets_type_to_Forbidden()
    {
        var error = Error.Forbidden("User.Forbidden", "Access denied");

        error.Code.Should().Be("User.Forbidden");
        error.Message.Should().Be("Access denied");
        error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Conflict_factory_sets_type_to_Conflict()
    {
        var error = Error.Conflict("User.Duplicate", "User already exists");

        error.Code.Should().Be("User.Duplicate");
        error.Message.Should().Be("User already exists");
        error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Unexpected_factory_sets_type_to_Unexpected()
    {
        var error = Error.Unexpected("Server.Error", "Something went wrong");

        error.Code.Should().Be("Server.Error");
        error.Message.Should().Be("Something went wrong");
        error.Type.Should().Be(ErrorType.Unexpected);
    }
}
