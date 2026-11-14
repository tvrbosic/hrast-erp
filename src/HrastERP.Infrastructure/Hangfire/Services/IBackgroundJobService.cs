using System.Linq.Expressions;

namespace HrastERP.Infrastructure.Hangfire;

/// <summary>
/// Enqueues and schedules Hangfire background jobs. Each enqueue/schedule method returns the Hangfire job ID.
/// Recurring jobs that should always exist are better defined as <see cref="IRecurringJobDefinition"/>.
/// </summary>
public interface IBackgroundJobService
{
    string Enqueue<T>(Expression<Func<T, Task>> methodCall);
    string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay);
    string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt);
    void AddOrUpdateRecurring<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression);
    void RemoveRecurring(string recurringJobId);
}
