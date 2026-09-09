using FluentAssertions;
using HrastERP.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Caching;

public class CacheSettingsTests
{
    [Fact]
    public void SectionName_IsCache()
    {
        CacheSettings.SectionName.Should().Be("Cache");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new CacheSettings();

        settings.ConnectionString.Should().Be(string.Empty);
        settings.DefaultTtlMinutes.Should().Be(60);
    }

    [Fact]
    public void BindsFromConfiguration()
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
    public void MissingConnectionString_FailsValidation()
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
    public void EmptyConnectionString_FailsValidation()
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
    public void DefaultTtlMinutes_OutOfRange_FailsValidation(int ttl)
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
