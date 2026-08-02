using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Hangfire;

internal static class RecurringJobRegistrar
{
    public static void RegisterAll(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var definitions = scope.ServiceProvider.GetServices<IRecurringJobDefinition>().ToList();

        var duplicateIds = definitions
            .GroupBy(d => d.JobId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
            throw new InvalidOperationException(
                $"Duplicate recurring job IDs detected: {string.Join(", ", duplicateIds)}");

        foreach (var definition in definitions)
        {
            var definitionType = definition.GetType();

            // Use RecurringJob.AddOrUpdate<TJob>(jobId, job => job.ExecuteAsync(CancellationToken.None), cron)
            // We need reflection because the concrete type is only known at runtime.
            var method = typeof(RecurringJob)
                .GetMethods()
                .First(m => m.Name == nameof(RecurringJob.AddOrUpdate)
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 4
                    && m.GetParameters()[0].ParameterType == typeof(string));

            var genericMethod = method.MakeGenericMethod(definitionType);

            // Build the expression: job => job.ExecuteAsync(CancellationToken.None)
            var parameter = System.Linq.Expressions.Expression.Parameter(definitionType, "job");
            var executeMethod = definitionType.GetMethod(nameof(IRecurringJobDefinition.ExecuteAsync))!;
            var call = System.Linq.Expressions.Expression.Call(
                parameter,
                executeMethod,
                System.Linq.Expressions.Expression.Constant(CancellationToken.None));
            var lambda = System.Linq.Expressions.Expression.Lambda(call, parameter);

            genericMethod.Invoke(null, [definition.JobId, lambda, definition.CronExpression, null]);
        }
    }
}
