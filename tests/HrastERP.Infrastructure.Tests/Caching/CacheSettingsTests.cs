using FluentAssertions;
using HrastERP.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Caching;

public class CacheSettingsTests
{
    [Fact]
    public void Has_correct_SectionName_When_class_is_defined()
    {
        CacheSettings.SectionName.Should().Be("Cache");
    }

    [Fact]
    public void Has_expected_default_values_When_instantiated()
    {
        var settings = new CacheSettings();

        settings.ConnectionString.Should().Be(string.Empty);
        settings.DefaultTtlMinutes.Should().Be(60);
    }

    [Fact]
    public void Binds_all_properties_When_configuration_section_is_present()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:ConnectionString"] = "redis-server:6380",
                ["Cache:DefaultTtlMinutes"] = "120"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<CacheSettings>()
            .BindConfiguration(CacheSettings.SectionName);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<CacheSettings>>().Value;

        settings.ConnectionString.Should().Be("redis-server:6380");
        settings.DefaultTtlMinutes.Should().Be(120);
    }

    [Fact]
    public void Fails_validation_When_ConnectionString_is_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:DefaultTtlMinutes"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<CacheSettings>()
            .BindConfiguration(CacheSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<CacheSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Fails_validation_When_ConnectionString_is_empty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:ConnectionString"] = "",
                ["Cache:DefaultTtlMinutes"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<CacheSettings>()
            .BindConfiguration(CacheSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<CacheSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Fails_validation_When_DefaultTtlMinutes_is_out_of_range(int ttl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:ConnectionString"] = "localhost:6379",
                ["Cache:DefaultTtlMinutes"] = ttl.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<CacheSettings>()
            .BindConfiguration(CacheSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<CacheSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
