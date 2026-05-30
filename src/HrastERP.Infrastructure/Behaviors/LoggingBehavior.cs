using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrastERP.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request execution with timing information.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Log that processing has started.
        logger.LogInformation("Handling {RequestName}", requestName);

        // Execute the next handler in the pipeline and measure elapsed time.
        var stopwatch = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        stopwatch.Stop();

        // Log completion with elapsed time for performance tracking.
        logger.LogInformation(
            "Handled {RequestName} in {ElapsedMilliseconds}ms",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
