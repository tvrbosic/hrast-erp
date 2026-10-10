using FluentAssertions;
using HrastERP.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Email;

public class MailKitEmailServiceTests
{
    private readonly MailKitEmailService _sut;

    public MailKitEmailServiceTests()
    {
        var settings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 587,
            Username = "test",
            Password = "test",
            FromAddress = "test@example.com",
            FromName = "Test"
        });

        _sut = new MailKitEmailService(settings, NullLogger<MailKitEmailService>.Instance);
    }

    [Fact]
    public void Returns_template_content_When_template_exists()
    {
        var result = _sut.LoadTemplate("welcome");

        result.Should().NotBeNull();
        result.Should().Contain("Welcome to Hrast ERP");
        result.Should().Contain("{{RecipientName}}");
    }

    [Fact]
    public void Returns_null_When_template_does_not_exist()
    {
        var result = _sut.LoadTemplate("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void Replaces_all_placeholders_When_all_keys_are_provided()
    {
        var template = "Hello {{Name}}, your email is {{Email}}.";
        var placeholders = new Dictionary<string, string>
        {
            ["Name"] = "John",
            ["Email"] = "john@example.com"
        };

        var result = MailKitEmailService.RenderTemplate(template, placeholders);

        result.Should().Be("Hello John, your email is john@example.com.");
    }

    [Fact]
    public void Leaves_unmatched_placeholders_intact_When_key_is_missing()
    {
        var template = "Hello {{Name}}, your role is {{Role}}.";
        var placeholders = new Dictionary<string, string>
        {
            ["Name"] = "John"
        };

        var result = MailKitEmailService.RenderTemplate(template, placeholders);

        result.Should().Be("Hello John, your role is {{Role}}.");
    }

    [Fact]
    public void Returns_template_unchanged_When_no_placeholders_provided()
    {
        var template = "Hello {{Name}}.";
        var placeholders = new Dictionary<string, string>();

        var result = MailKitEmailService.RenderTemplate(template, placeholders);

        result.Should().Be("Hello {{Name}}.");
    }
}
