using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Email;

public static class EmailErrors
{
    public static readonly Error SendFailed =
        Error.Unexpected("Email.SendFailed", "Failed to send email.");

    // A missing template is a deployment or code bug, not a resource the client asked for, so it is a 500
    public static readonly Error TemplateNotFound =
        Error.Unexpected("Email.TemplateNotFound", "The specified email template was not found.");
}
