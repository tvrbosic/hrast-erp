using HrastERP.API.Responses;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Diagnostics;

namespace HrastERP.API.Middleware;

/// <summary>
/// <c>UseStatusCodePages</c> handler that gives the framework's own bodiless error responses an
/// <see cref="ErrorResponse"/> body: 404 (no route matches), 405 (wrong HTTP method) and 415 (unsupported
/// <c>Content-Type</c>). ASP.NET Core only calls it for an error status with no body yet, so responses our code
/// already wrote (e.g. a handler's 404 <c>Tenant.NotFound</c>) are never touched. Other statuses are left as they are.
/// </summary>
public static class StatusCodeErrorResponse
{
    public static async Task WriteAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        var error = response.StatusCode switch
        {
            StatusCodes.Status404NotFound => GeneralErrors.RouteNotFound,
            StatusCodes.Status405MethodNotAllowed => GeneralErrors.MethodNotAllowed,
            StatusCodes.Status415UnsupportedMediaType => GeneralErrors.UnsupportedMediaType,
            _ => null
        };

        if (error is null)
            return;

        await response.WriteAsJsonAsync(new ErrorResponse(error.Code, error.Message));
    }
}
