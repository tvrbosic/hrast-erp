using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.Email;

public interface IEmailService
{
    Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default);

    Task<Result> SendTemplatedAsync(
        string templateName,
        Dictionary<string, string> placeholders,
        EmailMessage message,
        CancellationToken ct = default);
}
