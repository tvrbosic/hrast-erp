using FluentAssertions;
using HrastERP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Identity;

public class IdentitySettingsTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddIdentityServices();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Has_correct_SectionName_When_class_is_defined()
    {
        IdentitySettings.SectionName.Should().Be("Identity");
    }

    [Fact]
    public void Has_expected_default_values_When_instantiated()
    {
        var settings = new IdentitySettings();

        settings.PasswordRequiredLength.Should().Be(8);
        settings.PasswordRequireDigit.Should().BeTrue();
        settings.PasswordRequireLowercase.Should().BeTrue();
        settings.PasswordRequireUppercase.Should().BeTrue();
        settings.PasswordRequireNonAlphanumeric.Should().BeFalse();
        settings.LockoutMaxFailedAttempts.Should().Be(5);
        settings.LockoutMinutes.Should().Be(15);
    }

    [Fact]
    public void Applies_configured_values_to_IdentityOptions_When_AddIdentityServices_is_used()
    {
        // Arrange: configure values that differ from every default
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Identity:PasswordRequiredLength"] = "12",
            ["Identity:PasswordRequireDigit"] = "false",
            ["Identity:PasswordRequireNonAlphanumeric"] = "true",
            ["Identity:LockoutMaxFailedAttempts"] = "3",
            ["Identity:LockoutMinutes"] = "30"
        });

        // Act: resolve the options UserManager uses
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert: configured values are applied and the invariants kept in code stay on
        options.Password.RequiredLength.Should().Be(12);
        options.Password.RequireDigit.Should().BeFalse();
        options.Password.RequireNonAlphanumeric.Should().BeTrue();
        options.Lockout.MaxFailedAccessAttempts.Should().Be(3);
        options.Lockout.DefaultLockoutTimeSpan.Should().Be(TimeSpan.FromMinutes(30));
        options.User.RequireUniqueEmail.Should().BeTrue();
        options.Lockout.AllowedForNewUsers.Should().BeTrue();
    }

    [Theory]
    [InlineData("PasswordRequiredLength", "5")]
    [InlineData("PasswordRequiredLength", "129")]
    [InlineData("LockoutMaxFailedAttempts", "0")]
    [InlineData("LockoutMaxFailedAttempts", "101")]
    [InlineData("LockoutMinutes", "0")]
    [InlineData("LockoutMinutes", "1441")]
    public void Fails_validation_When_value_is_out_of_range(string key, string value)
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { [$"Identity:{key}"] = value });

        var act = () => provider.GetRequiredService<IOptions<IdentitySettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
