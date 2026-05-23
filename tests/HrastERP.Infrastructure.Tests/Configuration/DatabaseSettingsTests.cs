using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HrastERP.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Configuration;

public class DatabaseSettingsTests
{
    [Fact]
    public void DatabaseSettings_SectionName_IsDatabase()
    {
        DatabaseSettings.SectionName.Should().Be("Database");
    }

    [Fact]
    public void DatabaseSettings_BindsConnectionStringFromConfiguration()
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
    public void DatabaseSettings_DefaultConnectionString_IsEmptyString()
    {
        var settings = new DatabaseSettings();
        settings.ConnectionString.Should().Be(string.Empty);
    }

    [Fact]
    public void DatabaseSettings_MissingConnectionString_FailsValidation()
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
    public void DatabaseSettings_EmptyConnectionString_FailsValidation()
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
