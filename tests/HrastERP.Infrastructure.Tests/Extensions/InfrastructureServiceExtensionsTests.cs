using FluentAssertions;
using HrastERP.Infrastructure;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.FileStorage;
using HrastERP.Infrastructure.PdfGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Extensions;

public class InfrastructureServiceExtensionsTests
{
    [Fact]
    public void Registers_HrastDbContext_When_AddInfrastructure_is_called()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=test;Username=postgres;Password=postgres",
                ["Hangfire:SoftDeleteCleanupCron"] = "0 2 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 2 * * *",
                ["Cache:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HrastDbContext));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void Throws_OptionsValidationException_When_connection_string_is_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:SoftDeleteCleanupCron"] = "0 2 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 2 * * *",
                ["Cache:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Registers_IFileStorageService_When_AddInfrastructure_is_called()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=test;Username=postgres;Password=postgres",
                ["Hangfire:SoftDeleteCleanupCron"] = "0 2 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 2 * * *",
                ["Cache:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFileStorageService));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void Registers_ReportBuilder_When_AddInfrastructure_is_called()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=test;Username=postgres;Password=postgres",
                ["Hangfire:SoftDeleteCleanupCron"] = "0 2 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 2 * * *",
                ["Cache:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ReportBuilder));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void Registers_ICacheService_When_AddInfrastructure_is_called()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=test;Username=postgres;Password=postgres",
                ["Hangfire:SoftDeleteCleanupCron"] = "0 2 * * *",
                ["Hangfire:RefreshTokenCleanupCron"] = "0 2 * * *",
                ["Cache:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICacheService));
        descriptor.Should().NotBeNull();
    }
}
