using Hangfire.Dashboard;

namespace HrastERP.Infrastructure.Hangfire.Filters;

internal sealed class LocalhostDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        var localIp = httpContext.Connection.LocalIpAddress;

        if (remoteIp is null)
            return false;

        if (System.Net.IPAddress.IsLoopback(remoteIp))
            return true;

        return localIp is not null && remoteIp.Equals(localIp);
    }
}
