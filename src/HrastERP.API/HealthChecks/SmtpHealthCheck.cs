using System.Net.Sockets;
using HrastERP.Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HrastERP.API.HealthChecks;

public sealed class SmtpHealthCheck(IOptions<SmtpSettings> settings) : IHealthCheck
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(3);
    private readonly SmtpSettings _settings = settings.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(ConnectionTimeout);

            await client.ConnectAsync(_settings.Host, _settings.Port, cts.Token);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
