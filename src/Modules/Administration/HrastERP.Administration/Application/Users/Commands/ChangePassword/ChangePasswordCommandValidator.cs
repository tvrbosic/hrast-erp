using FluentValidation;

namespace HrastERP.Administration.Application.Users.Commands.ChangePassword;

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}
