using FluentAssertions;
using HrastERP.Administration.Application.Users;
using Microsoft.AspNetCore.Identity;

namespace Administration.Tests.Users;

public class IdentityFieldErrorsTests
{
    private static IdentityError Err(string code) => new() { Code = code, Description = $"{code} description" };

    [Fact]
    public void Groups_password_rules_under_camelCase_password_field_When_password_is_rejected()
    {
        var fieldErrors = IdentityFieldErrors.From(
            [Err("PasswordTooShort"), Err("PasswordRequiresDigit")], "NewPassword");

        fieldErrors.Should().ContainSingle().Which.Key.Should().Be("newPassword");
        fieldErrors["newPassword"].Should().Equal("PasswordTooShort description", "PasswordRequiresDigit description");
    }

    [Theory]
    [InlineData("InvalidEmail")]
    [InlineData("DuplicateEmail")]
    [InlineData("InvalidUserName")]
    [InlineData("DuplicateUserName")]
    public void Keys_error_under_email_When_email_or_user_name_is_rejected(string code)
    {
        var fieldErrors = IdentityFieldErrors.From([Err(code)], "Password");

        fieldErrors.Keys.Should().Equal("email");
    }

    [Fact]
    public void Keeps_camelCased_Identity_code_When_error_belongs_to_no_field()
    {
        var fieldErrors = IdentityFieldErrors.From([Err("ConcurrencyFailure")], "Password");

        fieldErrors.Keys.Should().Equal("concurrencyFailure");
    }
}
