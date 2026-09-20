using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using HrastERP.SharedKernel.Logging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrastERP.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request execution with timing information
/// and sanitized request payload (properties marked with [SensitiveData] are masked).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation("Handling {RequestName} with {@RequestData}", requestName, SanitizeRequest(request));

        var stopwatch = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        stopwatch.Stop();

        logger.LogInformation(
            "Handled {RequestName} in {ElapsedMilliseconds}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }

    private static Dictionary<string, object?> SanitizeRequest(TRequest request)
    {
        var properties = PropertyCache.GetOrAdd(
            typeof(TRequest),
            type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        var sanitized = new Dictionary<string, object?>(properties.Length);

        foreach (var property in properties)
        {
            var value = property.GetCustomAttribute<SensitiveDataAttribute>() is not null
                ? "***"
                : property.GetValue(request);

            sanitized[property.Name] = value;
        }

        return sanitized;
    }
}
