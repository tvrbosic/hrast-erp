using FluentValidation.TestHelper;
using HrastERP.Administration.Application.Roles.Commands.UpdateRole;

namespace Administration.Tests.Roles;

public class UpdateRoleCommandValidatorTests
{
    private readonly UpdateRoleCommandValidator _sut = new();

    private static UpdateRoleCommand Valid() =>
        new(Guid.NewGuid(), "QualityInspector", "Reads production", ["ProductionView"]);

    [Fact]
    public void Passes_When_command_is_valid()
    {
        _sut.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Fails_on_id_When_id_is_empty()
    {
        _sut.TestValidate(Valid() with { Id = Guid.Empty }).ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Fails_on_name_When_name_is_empty()
    {
        _sut.TestValidate(Valid() with { Name = "" }).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Fails_on_name_When_name_exceeds_100_characters()
    {
        _sut.TestValidate(Valid() with { Name = new string('a', 101) }).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Fails_on_description_When_description_exceeds_500_characters()
    {
        _sut.TestValidate(Valid() with { Description = new string('a', 501) })
            .ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Fails_on_permissions_When_permissions_are_null()
    {
        _sut.TestValidate(Valid() with { Permissions = null! }).ShouldHaveValidationErrorFor(x => x.Permissions);
    }

    [Fact]
    public void Fails_on_indexed_element_When_permission_name_is_invalid()
    {
        _sut.TestValidate(Valid() with { Permissions = ["financeview"] })
            .ShouldHaveValidationErrorFor("Permissions[0]");
    }
}
