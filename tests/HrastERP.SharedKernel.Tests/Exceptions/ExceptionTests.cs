using FluentAssertions;
using HrastERP.SharedKernel.Exceptions;

namespace HrastERP.SharedKernel.Tests.Exceptions;

public class ExceptionTests
{
    [Fact]
    public void NotFoundException_message_includes_entity_name_and_id()
    {
        var ex = new NotFoundException("User", 42);

        ex.Message.Should().Contain("User");
        ex.Message.Should().Contain("42");
    }

    [Fact]
    public void ForbiddenException_uses_default_message()
    {
        var ex = new ForbiddenException();

        ex.Message.Should().Be("Access is forbidden.");
    }

    [Fact]
    public void ForbiddenException_accepts_custom_message()
    {
        var ex = new ForbiddenException("You shall not pass.");

        ex.Message.Should().Be("You shall not pass.");
    }

    [Fact]
    public void ValidationException_message_is_generic_failure_message()
    {
        var errors = new Dictionary<string, string[]>
        {
            { "Email", ["Email is required", "Email must be valid"] }
        };
        var ex = new ValidationException(errors);

        ex.Message.Should().Be("One or more validation failures occurred.");
    }

    [Fact]
    public void ValidationException_exposes_errors_dictionary()
    {
        var errors = new Dictionary<string, string[]>
        {
            { "Email", ["Email is required"] },
            { "Name", ["Name is required"] }
        };
        var ex = new ValidationException(errors);

        ex.Errors.Should().HaveCount(2);
        ex.Errors["Email"].Should().ContainSingle("Email is required");
        ex.Errors["Name"].Should().ContainSingle("Name is required");
    }
}
