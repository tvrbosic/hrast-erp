namespace HrastERP.Infrastructure.FileStorage;

public sealed class FileDownload
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}
