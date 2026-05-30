using FluentAssertions;
using HrastERP.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Configuration;

public class JwtSettingsTests
{
    [Fact]
    public void JwtSettings_SectionName_IsJwt()
    {
        JwtSettings.SectionName.Should().Be("Jwt");
    }

    [Fact]
    public void JwtSettings_BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "ThisIsASecretKeyWithAtLeast32Chars!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:AccessTokenExpirationMinutes"] = "30",
                ["Jwt:RefreshTokenExpirationDays"] = "14"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        settings.SecretKey.Should().Be("ThisIsASecretKeyWithAtLeast32Chars!");
        settings.Issuer.Should().Be("TestIssuer");
        settings.Audience.Should().Be("TestAudience");
        settings.AccessTokenExpirationMinutes.Should().Be(30);
        settings.RefreshTokenExpirationDays.Should().Be(14);
    }

    [Fact]
    public void JwtSettings_Defaults_AreCorrect()
    {
        var settings = new JwtSettings();

        settings.SecretKey.Should().Be(string.Empty);
        settings.Issuer.Should().Be(string.Empty);
        settings.Audience.Should().Be(string.Empty);
        settings.AccessTokenExpirationMinutes.Should().Be(15);
        settings.RefreshTokenExpirationDays.Should().Be(7);
    }

    [Fact]
    public void JwtSettings_MissingSecretKey_FailsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void JwtSettings_ShortSecretKey_FailsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "TooShort",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
