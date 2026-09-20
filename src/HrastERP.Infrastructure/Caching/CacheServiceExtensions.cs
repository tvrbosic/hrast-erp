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

        // Register IDistributedCache backed by Redis.
        // Options are left empty here because AddStackExchangeRedisCache only accepts a plain
        // Action<RedisCacheOptions> with no access to DI services — we can't resolve the
        // IConnectionMultiplexer singleton registered above from inside that lambda.
        services.AddStackExchangeRedisCache(_ => { });

        // Patch RedisCacheOptions via AddOptions<T>().Configure<TDep>() — this overload
        // resolves TDep (IConnectionMultiplexer) from DI and passes it into the lambda,
        // letting us wire IDistributedCache to reuse the shared connection above instead
        // of opening a second one.
        // ConnectionMultiplexerFactory is Func<Task<IConnectionMultiplexer>>; Task.FromResult
        // wraps the already-created multiplexer in a completed Task to satisfy the async signature.
        services.AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((options, mux) =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(mux);
            });

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
