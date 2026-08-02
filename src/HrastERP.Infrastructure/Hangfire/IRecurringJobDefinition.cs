namespace HrastERP.Infrastructure.Hangfire;

public interface IRecurringJobDefinition
{
    string JobId { get; }
    string CronExpression { get; }
    Task ExecuteAsync(CancellationToken cancellationToken);
}