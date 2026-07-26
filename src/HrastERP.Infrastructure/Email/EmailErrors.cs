using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Email;

public static class EmailErrors
{
    public static readonly Error SendFailed =
        Error.Unexpected("Email.SendFailed", "Failed to send email.");

    public static readonly Error TemplateNotFound =
        Error.NotFound("Email.TemplateNotFound", "The specified email template was not found.");
}
