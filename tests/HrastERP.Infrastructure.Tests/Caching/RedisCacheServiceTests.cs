using FluentAssertions;
using HrastERP.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Caching;

public class RedisCacheServiceTests
{
    private readonly RedisCacheService _sut;

    public RedisCacheServiceTests()
    {
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        var settings = Options.Create(new CacheSettings
        {
            ConnectionString = "localhost:6379",
            DefaultTtlMinutes = 5
        });

        // IConnectionMultiplexer is null — RemoveByPrefixAsync tests verify graceful failure only.
        // Full prefix removal requires a live Redis connection.
        _sut = new RedisCacheService(
            cache,
            null!,
            settings,
            NullLogger<RedisCacheService>.Instance);
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _sut.GetAsync<string>("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredValue()
    {
        await _sut.SetAsync("test-key", "test-value");

        var result = await _sut.GetAsync<string>("test-key");

        result.Should().Be("test-value");
    }

    [Fact]
    public async Task SetAsync_ComplexObject_RoundTripsCorrectly()
    {
        var data = new TestData { Name = "test", Value = 42 };

        await _sut.SetAsync("complex-key", data);
        var result = await _sut.GetAsync<TestData>("complex-key");

        result.Should().NotBeNull();
        result!.Name.Should().Be("test");
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_RemovesFromCache()
    {
        await _sut.SetAsync("remove-me", "value");

        await _sut.RemoveAsync("remove-me");

        var result = await _sut.GetAsync<string>("remove-me");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithCustomExpiration_UsesProvidedTtl()
    {
        await _sut.SetAsync("short-ttl", "value", TimeSpan.FromMilliseconds(1));

        await Task.Delay(50);

        var result = await _sut.GetAsync<string>("short-ttl");
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WhenRedisUnavailable_DoesNotThrow()
    {
        var act = () => _sut.RemoveByPrefixAsync("some-prefix");

        await act.Should().NotThrowAsync();
    }

    private sealed class TestData
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
