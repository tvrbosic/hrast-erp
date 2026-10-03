using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using HrastERP.Infrastructure.Hangfire.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Tests.Hangfire;

public class RecurringJobRegistrarTests
{
    private sealed class FakeJob(string jobId, string cron) : IRecurringJobDefinition
    {
        public string JobId => jobId;
        public string CronExpression => cron;
        public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubRecurringJobManager : IRecurringJobManager
    {
        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
        {
        }

        public void RemoveIfExists(string recurringJobId) { }
        public void Trigger(string recurringJobId) { }
    }

    private static ServiceProvider BuildProvider(params FakeJob[] jobs)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRecurringJobManager>(new StubRecurringJobManager());
        foreach (var job in jobs)
            services.AddSingleton<IRecurringJobDefinition>(job);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void RegisterAll_DuplicateJobIds_ThrowsInvalidOperationException()
    {
        using var provider = BuildProvider(
            new FakeJob("duplicate-id", "0 * * * *"),
            new FakeJob("duplicate-id", "0 * * * *"));

        var act = () => RecurringJobRegistrar.RegisterAll(provider);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate-id*");
    }

    [Fact]
    public void RegisterAll_EmptyDefinitions_DoesNotThrow()
    {
        using var provider = BuildProvider();

        var act = () => RecurringJobRegistrar.RegisterAll(provider);

        act.Should().NotThrow();
    }
}
