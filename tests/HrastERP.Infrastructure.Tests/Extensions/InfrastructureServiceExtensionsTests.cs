using FluentAssertions;
using HrastERP.Infrastructure.Extensions;
using HrastERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HrastERP.Infrastructure.Configuration;

namespace HrastERP.Infrastructure.Tests.Extensions;

public class InfrastructureServiceExtensionsTests
{
    [Fact]
    public void AddInfrastructure_RegistersHrastDbContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=test;Username=postgres;Password=postgres"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HrastDbContext));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_MissingConnectionString_ThrowsOnOptionsResolution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
