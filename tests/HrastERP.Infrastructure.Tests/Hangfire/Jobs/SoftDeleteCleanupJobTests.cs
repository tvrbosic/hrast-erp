using FluentAssertions;
using HrastERP.Infrastructure.Hangfire;
using HrastERP.Infrastructure.Hangfire.Jobs;
using HrastERP.Infrastructure.Hangfire.Services;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Hangfire.Jobs;

public class SoftDeleteCleanupJobTests
{
    private static SoftDeleteCleanupJob CreateJob(HangfireSettings? settings = null)
    {
        settings ??= new HangfireSettings();
        var options = Options.Create(settings);

        var tenant = Substitute.For<ICurrentTenant>();
        var dbOptions = new DbContextOptionsBuilder<HrastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new HrastDbContext(dbOptions, [], tenant);

        return new SoftDeleteCleanupJob(dbContext, options, NullLogger<SoftDeleteCleanupJob>.Instance);
    }

    [Fact]
    public void Returns_correct_JobId_When_job_is_instantiated()
    {
        var job = CreateJob();

        job.JobId.Should().Be("infrastructure.soft-delete-cleanup");
    }

    [Fact]
    public void Returns_cron_from_settings_When_custom_value_is_configured()
    {
        var settings = new HangfireSettings { SoftDeleteCleanupCron = "30 3 * * *" };
        var job = CreateJob(settings);

        job.CronExpression.Should().Be("30 3 * * *");
    }

    [Fact]
    public void Returns_default_cron_When_settings_are_not_customized()
    {
        var job = CreateJob();

        job.CronExpression.Should().Be("0 2 * * *");
    }
}
