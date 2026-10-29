using Npgsql;
using Testcontainers.PostgreSql;

namespace HrastERP.Infrastructure.Tests.Fixtures;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    // Returns a connection string pointing to a fresh unique database on the shared Postgres instance.
    // Callers must call EnsureCreated() or MigrateAsync() to initialize the schema.
    public string NewConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"test_{Guid.NewGuid():N}"
        };
        return builder.ToString();
    }

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
