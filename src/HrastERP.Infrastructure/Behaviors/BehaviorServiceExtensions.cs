using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Behaviors;

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

        // TenantValidationBehavior short-circuits with Forbidden if no valid tenant context
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));

        // ValidationBehavior runs FluentValidation validators and returns Result.Failure on errors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
