using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HrastERP.API.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    Duration = entry.Value.Duration.ToString(@"hh\:mm\:ss\.fff"),
                    Description = entry.Value.Status != HealthStatus.Healthy
                        ? entry.Value.Description ?? entry.Value.Exception?.Message
                        : null
                }),
            TotalDuration = report.TotalDuration.ToString(@"hh\:mm\:ss\.fff")
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions));
    }
}
