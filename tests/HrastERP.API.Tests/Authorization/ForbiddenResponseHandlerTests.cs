using System.Text.Json;
using FluentAssertions;
using HrastERP.API.Authentication;
using HrastERP.API.Authorization;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.API.Tests.Authorization;

public class ForbiddenResponseHandlerTests
{
    private static readonly AuthorizationPolicy Policy =
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    private readonly ForbiddenResponseHandler _sut = new();
    private bool _nextCalled;

    private Task Next(HttpContext _)
    {
        _nextCalled = true;
        return Task.CompletedTask;
    }

    private static DefaultHttpContext CreateContext()
    {
        // JwtBearer is registered with the app's challenge handler so the default handler can challenge, as it does in the app.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o => o.Events = new JwtBearerEvents { OnChallenge = JwtChallengeResponse.WriteAsync });

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }

    [Fact]
    public async Task Writes_General_Forbidden_When_forbidden_without_error_reason()
    {
        var context = CreateContext();
        var failure = AuthorizationFailure.Failed([new PermissionRequirement(default)]);

        await _sut.HandleAsync(Next, context, Policy, PolicyAuthorizationResult.Forbid(failure));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var body = await ReadBodyAsync(context);
        body.GetProperty("code").GetString().Should().Be(GeneralErrors.Forbidden.Code);
        body.GetProperty("message").GetString().Should().Be(GeneralErrors.Forbidden.Message);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Writes_reason_error_When_forbidden_with_error_reason()
    {
        var context = CreateContext();
        var handler = new SuperAdminTenantAuthorizationHandler(null!);
        var failure = AuthorizationFailure.Failed([new ErrorFailureReason(handler, ApiErrors.SuperAdminTenantRequired)]);

        await _sut.HandleAsync(Next, context, Policy, PolicyAuthorizationResult.Forbid(failure));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var body = await ReadBodyAsync(context);
        body.GetProperty("code").GetString().Should().Be("Tenant.SuperAdminRequired");
    }

    [Fact]
    public async Task Writes_General_Unauthorized_When_challenged()
    {
        var context = CreateContext();

        await _sut.HandleAsync(Next, context, Policy, PolicyAuthorizationResult.Challenge());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var body = await ReadBodyAsync(context);
        body.GetProperty("code").GetString().Should().Be(GeneralErrors.Unauthorized.Code);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Calls_next_When_authorization_succeeds()
    {
        var context = CreateContext();

        await _sut.HandleAsync(Next, context, Policy, PolicyAuthorizationResult.Success());

        _nextCalled.Should().BeTrue();
    }
}
