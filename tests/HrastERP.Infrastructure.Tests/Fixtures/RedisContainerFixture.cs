using Testcontainers.Redis;

namespace HrastERP.Infrastructure.Tests.Fixtures;

public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
