namespace HrastERP.Infrastructure.Hangfire.Services;

/// <summary>
/// A recurring Hangfire job. <c>RecurringJobRegistrar</c> registers every implementation at startup.
/// Register each one in DI both as <see cref="IRecurringJobDefinition"/> (for discovery) and by its
/// concrete type (Hangfire's job activator resolves the concrete type).
/// </summary>
public interface IRecurringJobDefinition
{
    /// <summary>Must be unique across all jobs; startup fails on duplicates.</summary>
    string JobId { get; }

    string CronExpression { get; }

    /// <summary>Hangfire always calls this with <see cref="CancellationToken.None"/>.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
