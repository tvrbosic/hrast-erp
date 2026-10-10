using FluentAssertions;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HrastERP.Infrastructure.Tests.Caching;

[Trait("Category", "Integration")]
public class RedisCacheServiceTests : IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private readonly RedisContainerFixture _fixture;
    private IConnectionMultiplexer _multiplexer = null!;
    private RedisCacheService _sut = null!;

    public RedisCacheServiceTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);

        var cache = new RedisCache(Options.Create(new RedisCacheOptions
        {
            Configuration = _fixture.ConnectionString
        }));

        var settings = Options.Create(new CacheSettings
        {
            ConnectionString = _fixture.ConnectionString,
            DefaultTtlMinutes = 5
        });

        _sut = new RedisCacheService(cache, _multiplexer, settings, NullLogger<RedisCacheService>.Instance);
    }

    public Task DisposeAsync()
    {
        _multiplexer.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Returns_null_When_key_does_not_exist()
    {
        var result = await _sut.GetAsync<string>("nonexistent-" + Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Returns_stored_value_When_key_exists()
    {
        var key = "test-key-" + Guid.NewGuid();
        await _sut.SetAsync(key, "test-value");

        var result = await _sut.GetAsync<string>(key);

        result.Should().Be("test-value");
    }

    [Fact]
    public async Task Serializes_and_deserializes_complex_object_correctly_When_stored_and_retrieved()
    {
        var key = "complex-key-" + Guid.NewGuid();
        var data = new TestData { Name = "test", Value = 42 };

        await _sut.SetAsync(key, data);
        var result = await _sut.GetAsync<TestData>(key);

        result.Should().NotBeNull();
        result!.Name.Should().Be("test");
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task Removes_key_from_cache_When_key_exists()
    {
        var key = "remove-me-" + Guid.NewGuid();
        await _sut.SetAsync(key, "value");

        await _sut.RemoveAsync(key);

        var result = await _sut.GetAsync<string>(key);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Expires_key_When_custom_ttl_elapses()
    {
        var key = "short-ttl-" + Guid.NewGuid();
        await _sut.SetAsync(key, "value", TimeSpan.FromMilliseconds(100));

        await Task.Delay(500);

        var result = await _sut.GetAsync<string>(key);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Removes_all_matching_keys_When_prefix_matches()
    {
        var prefix = "test-prefix-" + Guid.NewGuid().ToString("N");
        await _sut.SetAsync($"{prefix}:key1", "value1");
        await _sut.SetAsync($"{prefix}:key2", "value2");

        await _sut.RemoveByPrefixAsync(prefix);

        var result1 = await _sut.GetAsync<string>($"{prefix}:key1");
        var result2 = await _sut.GetAsync<string>($"{prefix}:key2");
        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    private sealed class TestData
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
