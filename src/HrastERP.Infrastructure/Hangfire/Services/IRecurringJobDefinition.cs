namespace HrastERP.Infrastructure.Hangfire.Services;

public interface IRecurringJobDefinition
{
    string JobId { get; }
    string CronExpression { get; }
    Task ExecuteAsync(CancellationToken cancellationToken);
}
