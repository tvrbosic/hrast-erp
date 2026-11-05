using FluentAssertions;
using HrastERP.Administration;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.Hangfire;
using HrastERP.Infrastructure.Hangfire.Jobs;
using HrastERP.Infrastructure.Tests.Fixtures;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Hangfire.Jobs;

[Trait("Category", "Integration")]
public class RefreshTokenCleanupJobIntegrationTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly HrastDbContext _context;

    public RefreshTokenCleanupJobIntegrationTests(PostgresContainerFixture fixture)
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);

        _context = new HrastDbContext(
            new DbContextOptionsBuilder<HrastDbContext>().UseNpgsql(fixture.NewConnectionString()).Options,
            [new EntityConfigurationAssembly(typeof(AdministrationModule).Assembly)],
            tenant);
    }

    public async Task InitializeAsync() => await _context.Database.MigrateAsync();

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Deletes_expired_and_old_revoked_tokens_When_job_runs()
    {
        var settings = new HangfireSettings();
        var now = DateTime.UtcNow;
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _context.Set<Tenant>().Add(new Tenant(tenantId, "Test tenant"));
        _context.Users.Add(new ApplicationUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = "cleanup-user",
            NormalizedUserName = "CLEANUP-USER",
            Email = "cleanup@example.com",
            NormalizedEmail = "CLEANUP@EXAMPLE.COM",
            FirstName = "Clean",
            LastName = "Up"
        });

        RefreshToken Token(string value, DateTime expiresAt, DateTime? revokedAt = null) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Token = value,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            RevokedAt = revokedAt
        };

        var expired = Token("expired", now.AddDays(-1));
        var revoked = Token("revoked", now.AddDays(1), now.AddDays(-(settings.RevokedTokenRetentionDays + 1)));
        var active = Token("active", now.AddDays(1));
        _context.Set<RefreshToken>().AddRange(expired, revoked, active);
        await _context.SaveChangesAsync();

        var job = new RefreshTokenCleanupJob(
            _context, Options.Create(settings), NullLogger<RefreshTokenCleanupJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        var remaining = _context.Set<RefreshToken>().IgnoreQueryFilters().AsNoTracking().Select(t => t.Id).ToList();
        remaining.Should().ContainSingle().Which.Should().Be(active.Id);
    }
}
