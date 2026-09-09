using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HrastERP.Infrastructure.Caching;

/// <summary>
/// Redis-backed cache service. Wraps <see cref="IDistributedCache"/> to provide a typed
/// JSON API — consumers call <c>GetAsync&lt;T&gt;</c>/<c>SetAsync&lt;T&gt;</c> instead of
/// dealing with raw <c>byte[]</c> serialization at every call site.
/// <para/>
/// <see cref="IConnectionMultiplexer"/> is needed solely for <see cref="RemoveByPrefixAsync"/>,
/// which requires Redis-specific SCAN commands not available through <see cref="IDistributedCache"/>.
/// All other operations use the standard <see cref="IDistributedCache"/> interface.
/// </summary>
internal sealed class RedisCacheService(
    IDistributedCache cache,
    IConnectionMultiplexer redis,
    IOptions<CacheSettings> settings,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private readonly CacheSettings _settings = settings.Value;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken);
            if (bytes is null)
                return default;

            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache GET failed for key {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_settings.DefaultTtlMinutes)
            };

            await cache.SetAsync(key, bytes, options, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache SET failed for key {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache REMOVE failed for key {CacheKey}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all server nodes — single server in dev, multiple in a Redis cluster
            var endpoints = redis.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                // IServer exposes server-level commands (SCAN); must query each node since keys are distributed
                var server = redis.GetServer(endpoint);
                var keys = new List<RedisKey>();

                // KeysAsync uses SCAN internally — non-blocking incremental iteration, unlike KEYS
                await foreach (var key in server.KeysAsync(pattern: $"{prefix}:*"))
                {
                    keys.Add(key);
                }

                if (keys.Count > 0)
                {
                    // IDatabase handles data operations and routes to the correct node automatically
                    var db = redis.GetDatabase();
                    await db.KeyDeleteAsync(keys.ToArray());

                    logger.LogInformation(
                        "Cache prefix invalidation: removed {Count} keys matching {Prefix}:*",
                        keys.Count, prefix);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache REMOVE BY PREFIX failed for prefix {CachePrefix}", prefix);
        }
    }
}
