using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace HrastERP.Administration.Application.Users.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler(UserManager<ApplicationUser> userManager)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
            return Result.Failure(AdministrationErrors.UserNotFound);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!resetResult.Succeeded)
        {
            var errors = resetResult.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())
                as IReadOnlyDictionary<string, string[]>;
            return Result.Failure(Error.Validation("User.PasswordChangeFailed", "Failed to change password.", errors));
        }

        return Result.Success();
    }
}
