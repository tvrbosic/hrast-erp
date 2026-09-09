namespace HrastERP.Infrastructure.Caching;

/// <summary>
/// Typed cache abstraction over <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>.
/// Provides JSON serialization, configurable TTL, and prefix-based bulk invalidation.
/// All operations are non-throwing — failures are logged and return null/silently complete.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value by key. Returns <c>null</c> if the key does not exist,
    /// is expired, or deserialization fails.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a value in the cache with JSON serialization.
    /// When <paramref name="expiration"/> is <c>null</c>, uses <c>DefaultTtlMinutes</c> from settings.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a single key from the cache.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all keys matching <paramref name="prefix"/>:* from the cache.
    /// Uses Redis SCAN (non-blocking) — never KEYS.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
