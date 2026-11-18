using System.Net;
using System.Text.Json;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Results;

namespace HrastERP.API.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorResponseAsync(context);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var error = GeneralErrors.Unexpected;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(error.Code, error.Message), JsonOptions);
    }
}
