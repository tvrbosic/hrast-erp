using FluentValidation.TestHelper;
using HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;

namespace Administration.Tests.Tenants;

public class UpdateTenantCommandValidatorTests
{
    private readonly UpdateTenantCommandValidator _sut = new();

    [Fact]
    public void Passes_When_name_is_valid()
    {
        _sut.TestValidate(new UpdateTenantCommand(Guid.NewGuid(), "Acme")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Fails_on_name_When_name_is_whitespace_only()
    {
        _sut.TestValidate(new UpdateTenantCommand(Guid.NewGuid(), "   ")).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Fails_on_name_When_name_exceeds_200_characters()
    {
        _sut.TestValidate(new UpdateTenantCommand(Guid.NewGuid(), new string('a', 201)))
            .ShouldHaveValidationErrorFor(x => x.Name);
    }
}
