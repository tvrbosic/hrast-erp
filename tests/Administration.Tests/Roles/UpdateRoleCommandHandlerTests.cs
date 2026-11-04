using System.Globalization;
using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Roles;
using HrastERP.Administration.Application.Roles.Commands.UpdateRole;
using HrastERP.Infrastructure.Authorization;
using HrastERP.SharedKernel.Authorization;
using HrastERP.SharedKernel.Constants;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Administration.Tests.Roles;

[Trait("Category", "Integration")]
public class UpdateRoleCommandHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    private async Task<Result> HandleAsync(UpdateRoleCommand command)
    {
        await using var db = NewDbContext();
        return await new UpdateRoleCommandHandler(db).Handle(command, CancellationToken.None);
    }

    private async Task<Role> LoadAsync(Guid id)
    {
        await using var db = NewDbContext();
        return await db.Set<Role>().SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task Updates_all_fields_When_command_is_valid()
    {
        var role = await SeedRoleAsync("Auditor", Permission.FinanceView);

        var result = await HandleAsync(new UpdateRoleCommand(role.Id, "SeniorAuditor", "New", ["InventoryView"]));

        result.IsSuccess.Should().BeTrue();
        var updated = await LoadAsync(role.Id);
        updated.Name.Should().Be("SeniorAuditor");
        updated.Description.Should().Be("New");
        updated.Permissions.Should().Be(Permission.InventoryView);
    }

    [Fact]
    public async Task Returns_NotFound_When_role_does_not_exist()
    {
        var result = await HandleAsync(new UpdateRoleCommand(Guid.NewGuid(), "X", null, []));

        result.Error.Should().Be(AdministrationErrors.RoleNotFound);
    }

    [Fact]
    public async Task Returns_NameTaken_When_another_role_has_the_name()
    {
        await SeedRoleAsync("Auditor");
        var role = await SeedRoleAsync("Clerk");

        var result = await HandleAsync(new UpdateRoleCommand(role.Id, "auditor", null, []));

        result.Error.Should().Be(AdministrationErrors.RoleNameTaken);
    }

    [Fact]
    public async Task Succeeds_When_name_is_unchanged()
    {
        var role = await SeedRoleAsync("Auditor");

        var result = await HandleAsync(new UpdateRoleCommand(role.Id, "Auditor", "Changed", []));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Succeeds_When_Administrator_is_renamed_with_all_permissions()
    {
        await SeedRoleAsync("Administrator", PermissionMapper.All, RoleConstants.AdministratorRoleId);

        var result = await HandleAsync(new UpdateRoleCommand(
            RoleConstants.AdministratorRoleId, "Admin", null, PermissionMapper.ToNames(PermissionMapper.All)));

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(RoleConstants.AdministratorRoleId)).Name.Should().Be("Admin");
    }

    [Fact]
    public async Task Returns_AdministratorImmutable_When_Administrator_permissions_change()
    {
        await SeedRoleAsync("Administrator", PermissionMapper.All, RoleConstants.AdministratorRoleId);

        var result = await HandleAsync(new UpdateRoleCommand(
            RoleConstants.AdministratorRoleId, "Administrator", null, ["FinanceView"]));

        result.Error.Should().Be(AdministrationErrors.RoleAdministratorImmutable);
    }

    [Fact]
    public async Task Stores_trimmed_and_normalized_name_When_name_has_surrounding_whitespace()
    {
        var role = await SeedRoleAsync("Clerk");

        await HandleAsync(new UpdateRoleCommand(role.Id, " Senior Clerk  ", null, []));

        var updated = await LoadAsync(role.Id);
        updated.Name.Should().Be("Senior Clerk");
        updated.NormalizedName.Should().Be("SENIOR CLERK");
    }

    [Fact]
    public async Task Returns_NameTaken_When_another_role_name_differs_only_by_surrounding_whitespace()
    {
        await SeedRoleAsync("Auditor");
        var role = await SeedRoleAsync("Clerk");

        var result = await HandleAsync(new UpdateRoleCommand(role.Id, " Auditor", null, []));

        result.Error.Should().Be(AdministrationErrors.RoleNameTaken);
    }

    [Fact]
    public async Task Returns_NameTaken_When_another_role_name_differs_only_by_case_under_Turkish_culture()
    {
        await SeedRoleAsync("Administrator");
        var role = await SeedRoleAsync("Clerk");
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var result = await HandleAsync(new UpdateRoleCommand(role.Id, "ADMINISTRATOR", null, []));

            result.Error.Should().Be(AdministrationErrors.RoleNameTaken);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Throws_DbUpdateException_When_same_name_is_inserted_concurrently_after_check()
    {
        var role = await SeedRoleAsync("Clerk");
        var race = new BeforeSaveInterceptor(() => SeedRoleAsync("auditor"));
        await using var db = NewDbContext(interceptors: race);

        var act = () => new UpdateRoleCommandHandler(db)
            .Handle(new UpdateRoleCommand(role.Id, "Auditor", null, []), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verify = NewDbContext();
        (await verify.Set<Role>().SingleAsync(r => r.Id == role.Id)).Name.Should().Be("Clerk");
    }
}
