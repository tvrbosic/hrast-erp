using System.Text.Json;
using FluentAssertions;
using HrastERP.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace HrastERP.API.Tests.Middleware;

public class StatusCodeErrorResponseTests
{
    private static StatusCodeContext CreateContext(int statusCode)
    {
        var httpContext = new DefaultHttpContext { Response = { StatusCode = statusCode, Body = new MemoryStream() } };
        return new StatusCodeContext(httpContext, new StatusCodePagesOptions(), _ => Task.CompletedTask);
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound, "General.RouteNotFound")]
    [InlineData(StatusCodes.Status405MethodNotAllowed, "General.MethodNotAllowed")]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, "General.UnsupportedMediaType")]
    public async Task Writes_ErrorResponse_When_framework_returns_bodiless_client_error(int statusCode, string expectedCode)
    {
        var context = CreateContext(statusCode);

        await StatusCodeErrorResponse.WriteAsync(context);

        var response = context.HttpContext.Response;
        response.StatusCode.Should().Be(statusCode);
        response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(response.Body);
        body.GetProperty("code").GetString().Should().Be(expectedCode);
    }

    [Fact]
    public async Task Leaves_response_empty_When_status_is_not_mapped()
    {
        var context = CreateContext(StatusCodes.Status401Unauthorized);

        await StatusCodeErrorResponse.WriteAsync(context);

        context.HttpContext.Response.Body.Length.Should().Be(0);
    }
}
