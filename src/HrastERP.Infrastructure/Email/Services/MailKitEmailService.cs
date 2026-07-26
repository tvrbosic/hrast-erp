using HrastERP.SharedKernel.Results;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HrastERP.Infrastructure.Email;

internal sealed class MailKitEmailService(
    IOptions<SmtpSettings> settings,
    ILogger<MailKitEmailService> logger) : IEmailService
{
    private readonly SmtpSettings _settings = settings.Value;

    public async Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        try
        {
            using var email = BuildMimeMessage(message);
            using var client = new SmtpClient();

            await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl, ct);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            await client.SendAsync(email, ct);
            await client.DisconnectAsync(true, ct);

            logger.LogInformation("Email sent to {Recipients} with subject \"{Subject}\"",
                string.Join(", ", message.To), message.Subject);

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipients} with subject \"{Subject}\"",
                string.Join(", ", message.To), message.Subject);

            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    public async Task<Result> SendTemplatedAsync(
        string templateName,
        Dictionary<string, string> placeholders,
        EmailMessage message,
        CancellationToken ct = default)
    {
        var template = LoadTemplate(templateName);
        if (template is null)
            return Result.Failure(EmailErrors.TemplateNotFound);

        var renderedBody = RenderTemplate(template, placeholders);

        var templatedMessage = new EmailMessage
        {
            To = message.To,
            Cc = message.Cc,
            Bcc = message.Bcc,
            Subject = message.Subject,
            Body = renderedBody,
            IsHtml = message.IsHtml
        };

        return await SendAsync(templatedMessage, ct);
    }

    internal string? LoadTemplate(string templateName)
    {
        var assembly = typeof(MailKitEmailService).Assembly;
        var resourceName = $"HrastERP.Infrastructure.Email.Templates.{templateName}.html";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static string RenderTemplate(string template, Dictionary<string, string> placeholders)
    {
        var result = template;
        foreach (var (key, value) in placeholders)
        {
            result = result.Replace("{{" + key + "}}", value);
        }
        return result;
    }

    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));

        foreach (var to in message.To)
            email.To.Add(MailboxAddress.Parse(to));

        foreach (var cc in message.Cc)
            email.Cc.Add(MailboxAddress.Parse(cc));

        foreach (var bcc in message.Bcc)
            email.Bcc.Add(MailboxAddress.Parse(bcc));

        email.Subject = message.Subject;

        email.Body = message.IsHtml
            ? new TextPart("html") { Text = message.Body }
            : new TextPart("plain") { Text = message.Body };

        return email;
    }
}
