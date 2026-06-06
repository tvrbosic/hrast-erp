using System.Text.Json;
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
        // Skip validation if no validators are registered for this request type.
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        // Run all validators in parallel and collect their results.
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Flatten and filter out any null failures.
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        // No failures — proceed to the next handler in the pipeline.
        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        // Group failures by property name (camelCase) and build a field-level error dictionary.
        var fieldErrors = failures
            .GroupBy(f => JsonNamingPolicy.CamelCase.ConvertName(f.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());

        var error = Error.Validation("General.Validation", "One or more validation errors occurred.", fieldErrors);

        // We need to return a TResponse failure, but TResponse could be either Result or Result<TValue>.
        // For plain Result, we can call Result.Failure(error) directly.
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        // For Result<TValue> (e.g. Result<Order>), we can't write Result<Order>.Failure(error) because
        // we don't know the concrete TValue at compile time — TResponse is a generic type parameter.
        //
        // Reflection solves this: at runtime, typeof(TResponse) resolves to the concrete type
        // (e.g. Result<Order>), so we can look up its static Failure(Error) method and invoke it.
        // This is equivalent to writing Result<Order>.Failure(error), but discovered at runtime.
        var failureMethod = typeof(TResponse).GetMethod(
            nameof(Result.Failure),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            [typeof(Error)]);

        return (TResponse)failureMethod!.Invoke(null, [error])!;
    }
}
