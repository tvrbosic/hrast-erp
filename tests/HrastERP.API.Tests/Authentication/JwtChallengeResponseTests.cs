using System.Text.Json;
using FluentAssertions;
using HrastERP.API.Authentication;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace HrastERP.API.Tests.Authentication;

public class JwtChallengeResponseTests
{
    private static JwtBearerChallengeContext CreateContext(Exception? authenticateFailure)
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

        return new JwtBearerChallengeContext(httpContext, scheme, new JwtBearerOptions(), new AuthenticationProperties())
        {
            AuthenticateFailure = authenticateFailure
        };
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(response.Body);
    }

    [Fact]
    public async Task Writes_General_Unauthorized_When_no_token_is_sent()
    {
        var context = CreateContext(authenticateFailure: null);

        await JwtChallengeResponse.WriteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
        var body = await ReadBodyAsync(context.Response);
        body.GetProperty("code").GetString().Should().Be(GeneralErrors.Unauthorized.Code);
        context.Handled.Should().BeTrue();
    }

    [Fact]
    public async Task Writes_Auth_TokenExpired_When_token_has_expired()
    {
        var context = CreateContext(new SecurityTokenExpiredException("expired"));

        await JwtChallengeResponse.WriteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer error=\"invalid_token\"");
        var body = await ReadBodyAsync(context.Response);
        body.GetProperty("code").GetString().Should().Be(ApiErrors.TokenExpired.Code);
    }

    [Fact]
    public async Task Writes_General_Unauthorized_When_token_is_invalid()
    {
        var context = CreateContext(new SecurityTokenInvalidSignatureException("bad signature"));

        await JwtChallengeResponse.WriteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer error=\"invalid_token\"");
        var body = await ReadBodyAsync(context.Response);
        body.GetProperty("code").GetString().Should().Be(GeneralErrors.Unauthorized.Code);
    }
}
