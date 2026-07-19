using System.Linq.Expressions;
using Hangfire;

namespace HrastERP.Infrastructure.Hangfire;

internal sealed class HangfireBackgroundJobService : IBackgroundJobService
{
    public string Enqueue<T>(Expression<Func<T, Task>> methodCall)
        => BackgroundJob.Enqueue(methodCall);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay)
        => BackgroundJob.Schedule(methodCall, delay);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt)
        => BackgroundJob.Schedule(methodCall, enqueueAt);

    public void AddOrUpdateRecurring<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression)
        => RecurringJob.AddOrUpdate(recurringJobId, methodCall, cronExpression);

    public void RemoveRecurring(string recurringJobId)
        => RecurringJob.RemoveIfExists(recurringJobId);
}
