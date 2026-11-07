using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Users.Commands.CreateUser;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Administration.Tests.Users;

[Trait("Category", "Integration")]
public class UserEmailUniquenessTests(PostgresContainerFixture fixture)
    : RoleAssignmentTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result<Guid>> CreateAsync(Guid tenantId, string email)
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(tenantId);
        await using var db = NewDbContext(tenantId);
        var sut = new CreateUserCommandHandler(
            UserManagerThatSucceeds(), db, currentTenant, CurrentUserWith(TenantAdminPermissions));

        return await sut.Handle(
            new CreateUserCommand(email, "Passw0rd!", "New", "User", null, null), CancellationToken.None);
    }

    private async Task<Result> UpdateEmailAsync(Guid tenantId, ApplicationUser target, string email)
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(tenantId);
        await using var db = NewDbContext(tenantId);
        var sut = new UpdateUserCommandHandler(
            UserManagerThatSucceeds(), db, currentTenant, CurrentUserWith(TenantAdminPermissions));

        return await sut.Handle(
            new UpdateUserCommand(target.Id, email, target.FirstName, target.LastName, null), CancellationToken.None);
    }

    // No SoftDeleteInterceptor in this context, so set DeletedAt directly.
    private async Task SoftDeleteAsync(Guid userId)
    {
        await using var db = NewDbContext();
        await db.Set<ApplicationUser>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedAt, DateTime.UtcNow));
    }

    [Fact]
    public async Task Returns_UserEmailTaken_When_creating_user_with_email_of_soft_deleted_user()
    {
        var tenant = await SeedTenantAsync();
        var deleted = await SeedUserAsync(tenant.Id, roleId: null);
        await SoftDeleteAsync(deleted.Id);

        var result = await CreateAsync(tenant.Id, deleted.Email!);

        result.Error.Should().Be(AdministrationErrors.UserEmailTaken);
    }

    [Fact]
    public async Task Returns_UserEmailTaken_When_creating_user_with_email_used_in_another_tenant()
    {
        var tenant = await SeedTenantAsync();
        var otherTenant = await SeedTenantAsync("Other");
        var existing = await SeedUserAsync(otherTenant.Id, roleId: null);

        var result = await CreateAsync(tenant.Id, existing.Email!.ToUpperInvariant());

        result.Error.Should().Be(AdministrationErrors.UserEmailTaken);
    }

    [Fact]
    public async Task Returns_UserEmailTaken_When_changing_email_to_one_of_soft_deleted_user()
    {
        var tenant = await SeedTenantAsync();
        var target = await SeedUserAsync(tenant.Id, roleId: null);
        var deleted = await SeedUserAsync(tenant.Id, roleId: null);
        await SoftDeleteAsync(deleted.Id);

        var result = await UpdateEmailAsync(tenant.Id, target, deleted.Email!);

        result.Error.Should().Be(AdministrationErrors.UserEmailTaken);
    }

    [Fact]
    public async Task Updates_user_When_new_email_is_unused()
    {
        var tenant = await SeedTenantAsync();
        var target = await SeedUserAsync(tenant.Id, roleId: null);

        var result = await UpdateEmailAsync(tenant.Id, target, $"{Guid.NewGuid():N}@example.com");

        result.IsSuccess.Should().BeTrue();
    }
}
