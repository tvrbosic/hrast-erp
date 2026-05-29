using FluentValidation;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation validators before the handler executes.
/// If validation fails, short-circuits the pipeline and returns a <see cref="Result"/> failure
/// instead of throwing an exception.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
        var error = Error.Validation("General.Validation", errorMessages);

        // Create the appropriate Result failure type.
        // TResponse is constrained to Result, which could be Result or Result<TValue>.
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        // For Result<TValue>, call the static Failure method via reflection on the concrete type.
        var failureMethod = typeof(TResponse).GetMethod(
            nameof(Result.Failure),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            [typeof(Error)]);

        return (TResponse)failureMethod!.Invoke(null, [error])!;
    }
}
