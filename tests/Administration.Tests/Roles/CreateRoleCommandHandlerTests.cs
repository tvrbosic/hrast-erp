using System.Globalization;
using Administration.Tests.Fixtures;
using FluentAssertions;
using HrastERP.Administration.Application;
using HrastERP.Administration.Application.Roles.Commands.CreateRole;
using HrastERP.Infrastructure.Authorization;
using HrastERP.SharedKernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Administration.Tests.Roles;

[Trait("Category", "Integration")]
public class CreateRoleCommandHandlerTests(PostgresContainerFixture fixture)
    : IntegrationTestBase(fixture), IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task Creates_role_with_mapped_flags_When_name_is_unique()
    {
        await using var db = NewDbContext();
        var sut = new CreateRoleCommandHandler(db);

        var result = await sut.Handle(
            new CreateRoleCommand("QualityInspector", null, ["ProductionView", "InventoryView"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verify = NewDbContext();
        var role = await verify.Set<Role>().SingleAsync(r => r.Id == result.Value);
        role.Name.Should().Be("QualityInspector");
        role.Description.Should().BeEmpty();
        role.Permissions.Should().Be(Permission.ProductionView | Permission.InventoryView);
    }

    [Fact]
    public async Task Returns_NameTaken_When_name_differs_only_by_case()
    {
        await SeedRoleAsync("Auditor");
        await using var db = NewDbContext();
        var sut = new CreateRoleCommandHandler(db);

        var result = await sut.Handle(new CreateRoleCommand("AUDITOR", null, []), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdministrationErrors.RoleNameTaken);
    }

    [Fact]
    public async Task Stores_trimmed_and_normalized_name_When_name_has_surrounding_whitespace()
    {
        await using var db = NewDbContext();
        var sut = new CreateRoleCommandHandler(db);

        var result = await sut.Handle(new CreateRoleCommand("  Auditor ", null, []), CancellationToken.None);

        await using var verify = NewDbContext();
        var role = await verify.Set<Role>().SingleAsync(r => r.Id == result.Value);
        role.Name.Should().Be("Auditor");
        role.NormalizedName.Should().Be("AUDITOR");
    }

    [Fact]
    public async Task Returns_NameTaken_When_name_differs_only_by_surrounding_whitespace()
    {
        await SeedRoleAsync("Auditor");
        await using var db = NewDbContext();
        var sut = new CreateRoleCommandHandler(db);

        var result = await sut.Handle(new CreateRoleCommand("Auditor ", null, []), CancellationToken.None);

        result.Error.Should().Be(AdministrationErrors.RoleNameTaken);
    }

    [Fact]
    public async Task Returns_NameTaken_When_name_differs_only_by_case_under_Turkish_culture()
    {
        await SeedRoleAsync("Administrator");
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            await using var db = NewDbContext();
            var sut = new CreateRoleCommandHandler(db);

            var result = await sut.Handle(new CreateRoleCommand("ADMINISTRATOR", null, []), CancellationToken.None);

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
        var race = new BeforeSaveInterceptor(() => SeedRoleAsync("auditor"));
        await using var db = NewDbContext(interceptors: race);
        var sut = new CreateRoleCommandHandler(db);

        var act = () => sut.Handle(new CreateRoleCommand("Auditor", null, []), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        await using var verify = NewDbContext();
        (await verify.Set<Role>().CountAsync()).Should().Be(1);
    }
}
