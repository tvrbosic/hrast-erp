using FluentValidation;

namespace HrastERP.Administration.Application.Roles.Commands.CreateRole;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions)
            .Must(PermissionMapper.IsValidName)
            .WithMessage("'{PropertyValue}' is not a valid permission.");
    }
}
