using FluentAssertions;
using HrastERP.API.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace HrastERP.API.Tests.HealthChecks;

public class HealthCheckServiceExtensionsTests
{
    [Fact]
    public async Task Reports_redis_unhealthy_When_redis_connection_fails()
    {
        // Arrange: the shared multiplexer can't connect, as when Redis is down
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));
        services.AddHealthCheckServices();
        await using var provider = services.BuildServiceProvider();

        // Act: run only the redis check
        var report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == "redis");

        // Assert: the failure is reported as Unhealthy instead of escaping as an exception
        report.Entries["redis"].Status.Should().Be(HealthStatus.Unhealthy);
    }
}
