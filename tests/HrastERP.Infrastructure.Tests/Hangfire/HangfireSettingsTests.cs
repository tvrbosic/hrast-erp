using FluentAssertions;
using HrastERP.Infrastructure.Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Hangfire;

public class HangfireSettingsTests
{
    [Fact]
    public void Has_correct_SectionName_When_class_is_defined()
    {
        HangfireSettings.SectionName.Should().Be("Hangfire");
    }

    [Fact]
    public void Has_expected_default_values_When_instantiated()
    {
        var settings = new HangfireSettings();

        settings.WorkerCount.Should().Be(1);
        settings.SoftDeleteRetentionDays.Should().Be(90);
        settings.RevokedTokenRetentionDays.Should().Be(7);
        settings.SoftDeleteCleanupCron.Should().Be("0 2 * * *");
        settings.RefreshTokenCleanupCron.Should().Be("0 2 * * *");
    }

    [Fact]
    public void Binds_all_properties_When_configuration_section_is_present()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:WorkerCount"] = "4",
                ["Hangfire:SoftDeleteRetentionDays"] = "30",
                ["Hangfire:RevokedTokenRetentionDays"] = "14",
                ["Hangfire:SoftDeleteCleanupCron"] = "0 3 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 4 * * *"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<HangfireSettings>()
            .BindConfiguration(HangfireSettings.SectionName);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<HangfireSettings>>().Value;

        settings.WorkerCount.Should().Be(4);
        settings.SoftDeleteRetentionDays.Should().Be(30);
        settings.RevokedTokenRetentionDays.Should().Be(14);
        settings.SoftDeleteCleanupCron.Should().Be("0 3 * * *");
        settings.RefreshTokenCleanupCron.Should().Be("0 4 * * *");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Fails_validation_When_WorkerCount_is_out_of_range(int workerCount)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:WorkerCount"] = workerCount.ToString(),
                ["Hangfire:SoftDeleteCleanupCron"] = "0 2 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 2 * * *"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<HangfireSettings>()
            .BindConfiguration(HangfireSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<HangfireSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public void Fails_validation_When_SoftDeleteRetentionDays_is_out_of_range(int days)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:SoftDeleteRetentionDays"] = days.ToString(),
                ["Hangfire:SoftDeleteCleanupCron"] = "0 2 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 2 * * *"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<HangfireSettings>()
            .BindConfiguration(HangfireSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<HangfireSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
