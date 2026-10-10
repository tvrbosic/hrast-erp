using FluentAssertions;
using Hangfire;
using HrastERP.Infrastructure.Hangfire.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Hangfire;

public class RecurringJobRegistrarTests
{
    private static IRecurringJobDefinition CreateJobDefinition(string jobId, string cron = "0 * * * *")
    {
        var job = Substitute.For<IRecurringJobDefinition>();
        job.JobId.Returns(jobId);
        job.CronExpression.Returns(cron);
        return job;
    }

    private static ServiceProvider BuildProvider(params IRecurringJobDefinition[] jobs)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRecurringJobManager>(Substitute.For<IRecurringJobManager>());
        foreach (var job in jobs)
            services.AddSingleton<IRecurringJobDefinition>(job);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Throws_InvalidOperationException_When_duplicate_job_ids_are_registered()
    {
        using var provider = BuildProvider(
            CreateJobDefinition("duplicate-id"),
            CreateJobDefinition("duplicate-id"));

        var act = () => RecurringJobRegistrar.RegisterAll(provider);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate-id*");
    }

    [Fact]
    public void Does_not_throw_When_no_job_definitions_are_registered()
    {
        using var provider = BuildProvider();

        var act = () => RecurringJobRegistrar.RegisterAll(provider);

        act.Should().NotThrow();
    }
}
