using FluentAssertions;
using HrastERP.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Email;

public class SmtpSettingsTests
{
    [Fact]
    public void SectionName_IsSmtp()
    {
        SmtpSettings.SectionName.Should().Be("Smtp");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new SmtpSettings();

        settings.Host.Should().Be(string.Empty);
        settings.Port.Should().Be(587);
        settings.Username.Should().Be(string.Empty);
        settings.Password.Should().Be(string.Empty);
        settings.FromAddress.Should().Be(string.Empty);
        settings.FromName.Should().Be(string.Empty);
        settings.UseSsl.Should().BeTrue();
    }

    [Fact]
    public void BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.com",
                ["Smtp:Port"] = "465",
                ["Smtp:Username"] = "user@example.com",
                ["Smtp:Password"] = "secret",
                ["Smtp:FromAddress"] = "noreply@example.com",
                ["Smtp:FromName"] = "Test App",
                ["Smtp:UseSsl"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<SmtpSettings>()
            .BindConfiguration(SmtpSettings.SectionName);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<SmtpSettings>>().Value;

        settings.Host.Should().Be("smtp.example.com");
        settings.Port.Should().Be(465);
        settings.Username.Should().Be("user@example.com");
        settings.Password.Should().Be("secret");
        settings.FromAddress.Should().Be("noreply@example.com");
        settings.FromName.Should().Be("Test App");
        settings.UseSsl.Should().BeFalse();
    }

    [Theory]
    [InlineData("Smtp:Host")]
    [InlineData("Smtp:Username")]
    [InlineData("Smtp:Password")]
    [InlineData("Smtp:FromAddress")]
    [InlineData("Smtp:FromName")]
    public void MissingRequiredField_FailsValidation(string fieldToOmit)
    {
        var allFields = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Username"] = "user@example.com",
            ["Smtp:Password"] = "secret",
            ["Smtp:FromAddress"] = "noreply@example.com",
            ["Smtp:FromName"] = "Test App"
        };
        allFields.Remove(fieldToOmit);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(allFields)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<SmtpSettings>()
            .BindConfiguration(SmtpSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<SmtpSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Port_OutOfRange_FailsValidation(int port)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.com",
                ["Smtp:Port"] = port.ToString(),
                ["Smtp:Username"] = "user@example.com",
                ["Smtp:Password"] = "secret",
                ["Smtp:FromAddress"] = "noreply@example.com",
                ["Smtp:FromName"] = "Test App"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<SmtpSettings>()
            .BindConfiguration(SmtpSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<SmtpSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void InvalidFromAddress_FailsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.com",
                ["Smtp:Username"] = "user@example.com",
                ["Smtp:Password"] = "secret",
                ["Smtp:FromAddress"] = "not-an-email",
                ["Smtp:FromName"] = "Test App"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<SmtpSettings>()
            .BindConfiguration(SmtpSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<SmtpSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
