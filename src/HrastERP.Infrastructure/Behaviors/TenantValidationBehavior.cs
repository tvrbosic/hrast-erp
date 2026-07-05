using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Infrastructure.Behaviors;

public sealed class TenantValidationBehavior<TRequest, TResponse>(ICurrentTenant currentTenant)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId == Guid.Empty)
        {
            var error = Error.Forbidden("General.MissingTenant", "A valid tenant context is required.");

            if (typeof(TResponse) == typeof(Result))
                return (TResponse)Result.Failure(error);

            // For Result<TValue>, use reflection to call the static Failure method — same pattern as ValidationBehavior.
            var failureMethod = typeof(TResponse).GetMethod(
                nameof(Result.Failure),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                [typeof(Error)]);

            return (TResponse)failureMethod!.Invoke(null, [error])!;
        }

        return await next(cancellationToken);
    }
}
