using FluentAssertions;
using HrastERP.API.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.API.Tests.Cors;

public class CorsServiceExtensionsTests
{
    private const string AllowedOrigin = "https://app.example.com";

    private static ServiceProvider BuildProvider(params string[] origins)
    {
        var values = origins
            .Select((origin, index) => new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))
            .ToDictionary();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApiCors();
        return services.BuildServiceProvider();
    }

    private static CorsPolicy DefaultPolicy(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        return options.GetPolicy(options.DefaultPolicyName)!;
    }

    private static bool IsOriginAllowed(IServiceProvider provider, string origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = origin;
        return provider.GetRequiredService<ICorsService>().EvaluatePolicy(context, DefaultPolicy(provider)).IsOriginAllowed;
    }

    [Fact]
    public void Builds_default_policy_from_settings_When_origins_are_configured()
    {
        using var provider = BuildProvider(AllowedOrigin);

        var policy = DefaultPolicy(provider);

        policy.Origins.Should().Equal(AllowedOrigin);
        policy.ExposedHeaders.Should().Contain("Location");
        policy.PreflightMaxAge.Should().Be(TimeSpan.FromMinutes(10));
        policy.SupportsCredentials.Should().BeFalse();
        policy.AllowAnyHeader.Should().BeTrue();
        policy.AllowAnyMethod.Should().BeTrue();
    }

    [Theory]
    [InlineData(AllowedOrigin, true)]
    [InlineData("https://evil.example.com", false)]
    public void Evaluates_origin_When_request_has_Origin_header(string origin, bool allowed)
    {
        using var provider = BuildProvider(AllowedOrigin);

        IsOriginAllowed(provider, origin).Should().Be(allowed);
    }

    [Fact]
    public void Allows_no_origin_When_AllowedOrigins_is_empty()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IOptions<CorsSettings>>().Value.AllowedOrigins.Should().BeEmpty();
        IsOriginAllowed(provider, "http://localhost:5173").Should().BeFalse();
    }

    [Theory]
    [InlineData("http://localhost:5173/")]
    [InlineData("http://localhost:5173/app")]
    [InlineData("http://localhost:5173?x=1")]
    [InlineData("*")]
    [InlineData("ftp://example.com")]
    [InlineData("localhost:5173")]
    [InlineData("https://app.example.com:443")]
    [InlineData(" https://app.example.com")]
    [InlineData("https://app.example.com ")]
    [InlineData("https://user@app.example.com")]
    public void Fails_validation_When_origin_is_invalid(string origin)
    {
        using var provider = BuildProvider(origin);

        var act = () => provider.GetRequiredService<IOptions<CorsSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("HTTPS://App.Example.com")]
    [InlineData("http://[::1]:5173")]
    public void Passes_validation_When_origin_is_scheme_and_host_with_optional_port(string origin)
    {
        using var provider = BuildProvider(origin);

        provider.GetRequiredService<IOptions<CorsSettings>>().Value.AllowedOrigins.Should().Equal(origin);
    }
}
