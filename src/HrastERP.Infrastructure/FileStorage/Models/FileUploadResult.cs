namespace HrastERP.Infrastructure.FileStorage;

public sealed class FileUploadResult
{
    public required Guid FileId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeInBytes { get; init; }
}
