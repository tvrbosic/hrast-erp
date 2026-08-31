using FluentAssertions;
using HrastERP.Infrastructure.Hangfire;
using HrastERP.Infrastructure.Hangfire.Jobs;
using HrastERP.Infrastructure.Hangfire.Services;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.Hangfire.Jobs;

public class SoftDeleteCleanupJobTests
{
    private sealed class FakeCurrentTenant : ICurrentTenant
    {
        public Guid TenantId => Guid.Empty;
    }

    private static SoftDeleteCleanupJob CreateJob(HangfireSettings? settings = null)
    {
        settings ??= new HangfireSettings();
        var options = Options.Create(settings);

        var dbOptions = new DbContextOptionsBuilder<HrastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new HrastDbContext(dbOptions, [], new FakeCurrentTenant());

        return new SoftDeleteCleanupJob(dbContext, options, NullLogger<SoftDeleteCleanupJob>.Instance);
    }

    [Fact]
    public void JobId_Returns_CorrectValue()
    {
        var job = CreateJob();

        job.JobId.Should().Be("infrastructure.soft-delete-cleanup");
    }

    [Fact]
    public void CronExpression_Returns_ValueFromSettings()
    {
        var settings = new HangfireSettings { SoftDeleteCleanupCron = "30 3 * * *" };
        var job = CreateJob(settings);

        job.CronExpression.Should().Be("30 3 * * *");
    }

    [Fact]
    public void CronExpression_Returns_DefaultValue()
    {
        var job = CreateJob();

        job.CronExpression.Should().Be("0 2 * * *");
    }
}
