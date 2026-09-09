using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HrastERP.Infrastructure.Caching;

internal static class CacheServiceExtensions
{
    /// <summary>
    /// Registers Redis cache settings, connection, and the cache service.
    /// Called internally by <see cref="InfrastructureServiceExtensions.AddInfrastructure"/>.
    /// </summary>
    internal static IServiceCollection AddCacheServices(this IServiceCollection services)
    {
        services
            .AddOptions<CacheSettings>()
            .BindConfiguration(CacheSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Single shared TCP connection to Redis — IConnectionMultiplexer is designed to be
        // long-lived and multiplexed across concurrent operations.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<CacheSettings>>().Value;
            return ConnectionMultiplexer.Connect(settings.ConnectionString);
        });

        // Register IDistributedCache backed by Redis
        services.AddStackExchangeRedisCache(_ => { });

        // Wire IDistributedCache to reuse the shared IConnectionMultiplexer above
        // instead of creating a second Redis connection.
        // ConnectionMultiplexerFactory is Func<Task<IConnectionMultiplexer>> (no service provider),
        // so we use Configure<TDep> to inject the singleton.
        services.AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((options, mux) =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(mux);
            });

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
