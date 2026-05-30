using HrastERP.Infrastructure.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Extensions;

internal static class BehaviorServiceExtensions
{
    /// <summary>
    /// Registers MediatR pipeline behaviors for validation and logging.
    /// Called internally by <see cref="InfrastructureServiceExtensions.AddInfrastructure"/>.
    /// </summary>
    internal static IServiceCollection AddMediatRPipelineBehaviors(this IServiceCollection services)
    {
        // LoggingBehavior logs request/response with timing for all handlers
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // ValidationBehavior runs FluentValidation validators and returns Result.Failure on errors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
