using FluentAssertions;
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

    [Fact]
    public void RegisterAll_DuplicateJobIds_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRecurringJobDefinition>(new FakeJob("duplicate-id", "0 * * * *"));
        services.AddSingleton<IRecurringJobDefinition>(new FakeJob("duplicate-id", "0 * * * *"));
        var provider = services.BuildServiceProvider();

        var act = () => RecurringJobRegistrar.RegisterAll(provider);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate-id*");
    }

    [Fact]
    public void RegisterAll_EmptyDefinitions_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var act = () => RecurringJobRegistrar.RegisterAll(provider);

        act.Should().NotThrow();
    }
}
