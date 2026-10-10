using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HrastERP.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Configuration;

public class DatabaseSettingsTests
{
    [Fact]
    public void Has_correct_SectionName_When_class_is_defined()
    {
        DatabaseSettings.SectionName.Should().Be("Database");
    }

    [Fact]
    public void Binds_all_properties_When_configuration_section_is_present()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Database=test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<DatabaseSettings>()
            .BindConfiguration(DatabaseSettings.SectionName);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        settings.ConnectionString.Should().Be("Host=localhost;Database=test");
    }

    [Fact]
    public void Has_expected_default_values_When_instantiated()
    {
        var settings = new DatabaseSettings();
        settings.ConnectionString.Should().Be(string.Empty);
    }

    [Fact]
    public void Fails_validation_When_ConnectionString_is_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<DatabaseSettings>()
            .BindConfiguration(DatabaseSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Fails_validation_When_ConnectionString_is_empty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<DatabaseSettings>()
            .BindConfiguration(DatabaseSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
