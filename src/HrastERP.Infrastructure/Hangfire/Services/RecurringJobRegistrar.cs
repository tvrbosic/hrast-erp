using System.Linq.Expressions;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Hangfire.Services;

internal static class RecurringJobRegistrar
{
    public static void RegisterAll(IServiceProvider serviceProvider)
    {
        // Phase 1: Resolve all IRecurringJobDefinition implementations and IRecurringJobManager from DI.
        // CreateScope() is needed because scoped services can't be resolved from the root provider
        // (there's no HTTP request scope at startup).
        using var scope = serviceProvider.CreateScope();
        var definitions = scope.ServiceProvider.GetServices<IRecurringJobDefinition>().ToList();
        var jobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        // Phase 2: Validate that no two jobs share the same JobId.
        var duplicateIds = definitions
            .GroupBy(d => d.JobId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate recurring job IDs detected: {string.Join(", ", duplicateIds)}");
        }

        // Phase 3: Register each job with Hangfire.
        // Uses IRecurringJobManager (from DI) instead of the static RecurringJob class,
        // because JobStorage is only available after the DI container is built.
        foreach (var definition in definitions)
        {
            var definitionType = definition.GetType();

            // Phase 3a: Build the expression tree: job => job.ExecuteAsync(CancellationToken.None)
            // Hangfire serializes this expression and invokes it when the cron schedule fires.
            var parameter = Expression.Parameter(definitionType, "job");
            var executeMethod = definitionType.GetMethod(nameof(IRecurringJobDefinition.ExecuteAsync))!;
            var call = Expression.Call(
                parameter,
                executeMethod,
                Expression.Constant(CancellationToken.None));
            var lambda = Expression.Lambda(call, parameter);

            // Phase 3b: Convert the expression to a Hangfire Job using Job.FromExpression.
            // Reflection is needed because Job.FromExpression<T>() requires a compile-time generic type,
            // but the concrete job type is only known at runtime.
            // Must pick the async overload: FromExpression<T>(Expression<Func<T, Task>>)
            var fromExpressionMethod = typeof(Job)
                .GetMethods()
                .First(m => m.Name == nameof(Job.FromExpression)
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType.IsGenericType
                    && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                    && m.GetParameters()[0].ParameterType.GetGenericArguments()[0].IsGenericType
                    && m.GetParameters()[0].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>));

            var genericFromExpression = fromExpressionMethod.MakeGenericMethod(definitionType);
            var job = (Job)genericFromExpression.Invoke(null, [lambda])!;

            // Phase 3c: Register with IRecurringJobManager (no reflection needed here).
            jobManager.AddOrUpdate(definition.JobId, job, definition.CronExpression);
        }
    }
}
