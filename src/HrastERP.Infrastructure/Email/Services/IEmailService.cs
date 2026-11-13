using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Email;

/// <summary>
/// Sends email over SMTP. Failures are logged and returned as <c>Email.SendFailed</c>, never thrown.
/// </summary>
public interface IEmailService
{
    Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>
    /// Renders the embedded template <c>Email/Templates/{templateName}.html</c>, replacing each
    /// <c>{{key}}</c> with its value, and sends it. The rendered template replaces
    /// <paramref name="message"/>'s body. Returns <c>Email.TemplateNotFound</c> for an unknown template.
    /// </summary>
    Task<Result> SendTemplatedAsync(
        string templateName,
        Dictionary<string, string> placeholders,
        EmailMessage message,
        CancellationToken ct = default);
}
