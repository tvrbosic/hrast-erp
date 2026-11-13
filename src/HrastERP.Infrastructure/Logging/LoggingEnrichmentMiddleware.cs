using HrastERP.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace HrastERP.Infrastructure.Logging;

/// <summary>
/// Adds <c>UserId</c> and <c>TenantId</c> to every log event written during the request.
/// </summary>
public class LoggingEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, ICurrentTenant currentTenant)
    {
        using (LogContext.PushProperty("UserId", currentUser.UserId))
        using (LogContext.PushProperty("TenantId", currentTenant.TenantId))
        {
            await next(context);
        }
    }
}
