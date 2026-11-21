using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using HrastERP.Infrastructure.Identity;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Administration.Tests.Users;

[Trait("Category", "Integration")]
public class UpdateUserEmailTests(PostgresContainerFixture fixture)
    : RoleAssignmentTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private static readonly IdentityError ConcurrencyFailure =
        new() { Code = "ConcurrencyFailure", Description = "Optimistic concurrency failure." };

    private async Task<Result> HandleAsync(UserManager<ApplicationUser> userManager, ApplicationUser target)
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.TenantId.Returns(target.TenantId);
        await using var db = NewDbContext(target.TenantId);
        var sut = new UpdateUserCommandHandler(
            userManager, db, currentTenant, CurrentUserWith(Permission.AdministrationEdit));

        return await sut.Handle(
            new UpdateUserCommand(target.Id, $"{Guid.NewGuid():N}@example.com", target.FirstName, target.LastName,
                target.RoleId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Returns_UpdateFailed_with_Identity_errors_When_SetEmailAsync_fails()
    {
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync(tenant.Id, roleId: null);
        var userManager = UserManagerThatSucceeds();
        userManager.SetEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(ConcurrencyFailure));

        var result = await HandleAsync(userManager, user);

        result.Error.Code.Should().Be("User.UpdateFailed");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.ValidationErrors.Should().ContainKey("concurrencyFailure");
    }

    [Fact]
    public async Task Returns_UpdateFailed_with_Identity_errors_When_SetUserNameAsync_fails()
    {
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync(tenant.Id, roleId: null);
        var userManager = UserManagerThatSucceeds();
        userManager.SetUserNameAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(
                new IdentityError { Code = "InvalidUserName", Description = "User name is invalid." }));

        var result = await HandleAsync(userManager, user);

        result.Error.Code.Should().Be("User.UpdateFailed");
        result.Error.ValidationErrors.Should().ContainKey("email");
    }
}
