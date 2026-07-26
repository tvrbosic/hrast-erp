namespace HrastERP.Infrastructure.Email;

public sealed class EmailMessage
{
    public required List<string> To { get; init; }
    public List<string> Cc { get; init; } = [];
    public List<string> Bcc { get; init; } = [];
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public bool IsHtml { get; init; } = true;
}
