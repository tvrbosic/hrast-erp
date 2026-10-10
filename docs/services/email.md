# Email

MailKit provides SMTP email delivery with support for plain sends and HTML template rendering. The service runs in-process — no separate mail worker needed.

## Configuration

Settings in `appsettings.json` under `"Smtp"`:

```json
{
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "Username": "user@example.com",
    "Password": "secret",
    "FromAddress": "noreply@hrast-erp.com",
    "FromName": "Hrast ERP",
    "UseSsl": true
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `Host` | string | — | SMTP server hostname (required) |
| `Port` | int (1–65535) | 587 | SMTP port |
| `Username` | string | — | SMTP auth username (required) |
| `Password` | string | — | SMTP auth password (required) |
| `FromAddress` | string | — | Sender email address — must be valid (required) |
| `FromName` | string | — | Display name in From header (required) |
| `UseSsl` | bool | true | Whether to use SSL/TLS |

Bound to `SmtpSettings` via `ValidateDataAnnotations()` + `ValidateOnStart()` — misconfiguration fails at startup.

## Development Setup

Development config uses localhost:1025 (MailHog or similar local SMTP server). No real emails are sent. Run MailHog with Docker:

```bash
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
```

Captured emails are visible at `http://localhost:8025`.

## Sending an Email from a Handler

Inject `IEmailService` and call `SendAsync` or `SendTemplatedAsync`. Both return `Result`.

**Plain send:**

```csharp
internal sealed class MyCommandHandler(IEmailService emailService) : IRequestHandler<MyCommand, Result>
{
    public async Task<Result> Handle(MyCommand command, CancellationToken ct)
    {
        var message = new EmailMessage
        {
            To = [command.RecipientEmail],
            Subject = "Your order has been confirmed",
            Body = "<p>Thank you for your order.</p>",
            IsHtml = true
        };

        return await emailService.SendAsync(message, ct);
    }
}
```

**Templated send:**

```csharp
var message = new EmailMessage
{
    To = [user.Email],
    Subject = "Welcome to Hrast ERP"
};

return await emailService.SendTemplatedAsync(
    templateName: "welcome",
    placeholders: new Dictionary<string, string>
    {
        ["RecipientName"] = user.FullName,
        ["Email"] = user.Email
    },
    message: message,
    ct: ct);
```

`SendTemplatedAsync` loads the named template, substitutes placeholders, then delegates to `SendAsync`.

## Adding a New HTML Template

1. **Create the template file** in `src/HrastERP.Infrastructure/Email/Templates/`:

```html
<!DOCTYPE html>
<html>
<head><meta charset="utf-8" /><title>{{Subject}}</title></head>
<body>
    <p>Hello {{RecipientName}},</p>
    <p>Your message here.</p>
</body>
</html>
```

Use `{{PlaceholderName}}` syntax for substitution. Placeholder names are case-sensitive.

2. **Done.** Templates are embedded via the `.csproj` glob `Email\Templates\**\*.html`. No registration needed.

Call the template by filename without extension: `"welcome"` loads `welcome.html`.

### Placeholder Behavior

- All occurrences of `{{Key}}` in the template are replaced.
- Unmatched placeholders (no key in the dictionary) are left in the output unchanged.
- Extra keys in the dictionary with no matching placeholder are silently ignored.