namespace HrastERP.Infrastructure.FileStorage;

public sealed class FileDownloadResult
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}
