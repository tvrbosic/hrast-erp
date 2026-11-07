using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Roles;
using HrastERP.Administration.Application.Roles.Commands.DeleteRole;
using HrastERP.Administration.Application.Users.Commands.ActivateUser;
using HrastERP.Administration.Application.Users.Commands.ChangePassword;
using HrastERP.Administration.Application.Users.Commands.DeleteUser;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using HrastERP.Administration.Application.Users.Queries.GetUserById;
using HrastERP.Administration.Application.Users.Queries.GetUsers;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Authorization;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Administration.Tests.Users;

[Trait("Category", "Integration")]
public class DeleteUserCommandHandlerTests(PostgresContainerFixture fixture)
    : RoleAssignmentTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    private async Task<Result> HandleAsync(
        Guid callerTenantId, Permission callerPermissions, Guid targetId, Guid? callerUserId = null)
    {
        var currentUser = CurrentUserWith(callerPermissions);
        currentUser.UserId.Returns(callerUserId ?? Guid.NewGuid());
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(callerTenantId);

        // The interceptor turns Remove into a soft delete; without it this would be a real DELETE.
        await using var db = NewDbContext(callerTenantId, new SoftDeleteInterceptor(currentUser));
        var sut = new DeleteUserCommandHandler(db, currentUser, currentTenant, _cache);

        return await sut.Handle(new DeleteUserCommand(targetId), CancellationToken.None);
    }

    private Task<Result> HandleAsSuperAdminAsync(Guid targetId) =>
        HandleAsync(TenantConstants.SuperAdminTenantId, PermissionMapper.All, targetId);

    private async Task<ApplicationUser?> LoadIncludingDeletedAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await db.Set<ApplicationUser>().IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Id == id);
    }

    private async Task SeedTokenAsync(ApplicationUser user, bool revoked)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = revoked ? DateTime.UtcNow : null
        };

        await using var db = NewDbContext();
        db.Set<RefreshToken>().Add(token);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Soft_deletes_deactivates_and_keeps_role_When_user_is_deleted()
    {
        var tenant = await SeedTenantAsync();
        var role = await SeedRoleAsync("Clerk", Permission.FinanceView);
        var user = await SeedUserAsync(tenant.Id, role.Id);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, user.Id);

        result.IsSuccess.Should().BeTrue();
        var stored = await LoadIncludingDeletedAsync(user.Id);
        stored.Should().NotBeNull();
        stored!.DeletedAt.Should().NotBeNull();
        stored.IsActive.Should().BeFalse();
        stored.RoleId.Should().Be(role.Id);
    }

    [Fact]
    public async Task Deletes_all_refresh_tokens_When_user_is_deleted()
    {
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync(tenant.Id, roleId: null);
        await SeedTokenAsync(user, revoked: false);
        await SeedTokenAsync(user, revoked: true);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, user.Id);

        result.IsSuccess.Should().BeTrue();
        await using var db = NewDbContext();
        (await db.Set<RefreshToken>().IgnoreQueryFilters().CountAsync(t => t.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Removes_active_status_cache_entry_When_user_is_deleted()
    {
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync(tenant.Id, roleId: null);

        await HandleAsync(tenant.Id, TenantAdminPermissions, user.Id);

        await _cache.Received().RemoveAsync($"Users:active:{user.Id}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_NotFound_When_user_does_not_exist()
    {
        var result = await HandleAsSuperAdminAsync(Guid.NewGuid());

        result.Error.Should().Be(AdministrationErrors.UserNotFound);
    }

    [Fact]
    public async Task Returns_NotFound_When_user_is_already_deleted()
    {
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync(tenant.Id, roleId: null);
        await HandleAsSuperAdminAsync(user.Id);

        var result = await HandleAsSuperAdminAsync(user.Id);

        result.Error.Should().Be(AdministrationErrors.UserNotFound);
    }

    [Fact]
    public async Task Returns_NotFound_When_tenant_admin_deletes_user_of_another_tenant()
    {
        var own = await SeedTenantAsync("Own");
        var other = await SeedTenantAsync("Other");
        var user = await SeedUserAsync(other.Id, roleId: null);

        var result = await HandleAsync(own.Id, TenantAdminPermissions, user.Id);

        result.Error.Should().Be(AdministrationErrors.UserNotFound);
        (await LoadIncludingDeletedAsync(user.Id))!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Returns_CannotDeleteSelf_When_caller_deletes_own_account()
    {
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync(tenant.Id, roleId: null);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, user.Id, callerUserId: user.Id);

        result.Error.Should().Be(AdministrationErrors.UserCannotDeleteSelf);
        (await LoadIncludingDeletedAsync(user.Id))!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Returns_DeleteExceedsOwnPermissions_When_tenant_admin_deletes_user_with_more_permissions()
    {
        var tenant = await SeedTenantAsync();
        var manager = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);
        var user = await SeedUserAsync(tenant.Id, manager.Id);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, user.Id);

        result.Error.Should().Be(AdministrationErrors.UserDeleteExceedsOwnPermissions);
        var stored = await LoadIncludingDeletedAsync(user.Id);
        stored!.DeletedAt.Should().BeNull();
        stored.IsActive.Should().BeTrue();
        stored.RoleId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task Deletes_user_When_super_admin_deletes_user_with_more_permissions()
    {
        var tenant = await SeedTenantAsync();
        var manager = await SeedRoleAsync("FinanceManager", Permission.FinanceView | Permission.FinanceDelete);
        var user = await SeedUserAsync(tenant.Id, manager.Id);

        var result = await HandleAsync(
            TenantConstants.SuperAdminTenantId, Permission.AdministrationDelete, user.Id);

        result.IsSuccess.Should().BeTrue();
        (await LoadIncludingDeletedAsync(user.Id))!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Deletes_user_When_tenant_admin_deletes_peer_with_same_permissions()
    {
        var tenant = await SeedTenantAsync();
        var peerRole = await SeedRoleAsync("Peer", TenantAdminPermissions);
        var user = await SeedUserAsync(tenant.Id, peerRole.Id);

        var result = await HandleAsync(tenant.Id, TenantAdminPermissions, user.Id);

        result.IsSuccess.Should().BeTrue();
        (await LoadIncludingDeletedAsync(user.Id))!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Refuses_deleting_role_When_its_only_user_is_soft_deleted()
    {
        var tenant = await SeedTenantAsync();
        var role = await SeedRoleAsync("Temp", Permission.FinanceView);
        var user = await SeedUserAsync(tenant.Id, role.Id);
        await HandleAsync(tenant.Id, TenantAdminPermissions, user.Id);

        await using var db = NewDbContext();
        var result = await new DeleteRoleCommandHandler(db)
            .Handle(new DeleteRoleCommand(role.Id), CancellationToken.None);

        result.Error.Should().Be(AdministrationErrors.RoleInUse(1));
    }

    [Fact]
    public async Task Returns_NotFound_from_other_user_commands_When_user_is_deleted()
    {
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync(tenant.Id, roleId: null);
        await HandleAsSuperAdminAsync(user.Id);

        await using var db = NewDbContext();
        var userManager = new UserManager<ApplicationUser>(
            new UserOnlyStore<ApplicationUser, HrastDbContext, Guid>(db),
            null!, null!, null!, null!, null!, null!, null!, null!);

        var activate = await new ActivateUserCommandHandler(db, _cache)
            .Handle(new ActivateUserCommand(user.Id), CancellationToken.None);
        var getById = await new GetUserByIdQueryHandler(db)
            .Handle(new GetUserByIdQuery(user.Id), CancellationToken.None);
        var changePassword = await new ChangePasswordCommandHandler(userManager)
            .Handle(new ChangePasswordCommand(user.Id, "Passw0rd!"), CancellationToken.None);

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(TenantConstants.SuperAdminTenantId);
        var update = await new UpdateUserCommandHandler(
                UserManagerThatSucceeds(), db, currentTenant, CurrentUserWith(PermissionMapper.All))
            .Handle(new UpdateUserCommand(user.Id, "x@example.com", "First", "Last", null), CancellationToken.None);

        update.Error.Should().Be(AdministrationErrors.UserNotFound);
        activate.Error.Should().Be(AdministrationErrors.UserNotFound);
        getById.Error.Should().Be(AdministrationErrors.UserNotFound);
        changePassword.Error.Should().Be(AdministrationErrors.UserNotFound);
    }

    [Fact]
    public async Task Leaves_user_out_of_list_When_user_is_deleted()
    {
        var tenant = await SeedTenantAsync();
        var kept = await SeedUserAsync(tenant.Id, roleId: null);
        var deleted = await SeedUserAsync(tenant.Id, roleId: null);
        await HandleAsSuperAdminAsync(deleted.Id);

        await using var db = NewDbContext();
        var result = await new GetUsersQueryHandler(db)
            .Handle(new GetUsersQuery(1, 100, null, null), CancellationToken.None);

        var ids = result.Value.Items.Select(u => u.Id).ToList();
        ids.Should().Contain(kept.Id);
        ids.Should().NotContain(deleted.Id);
    }
}
