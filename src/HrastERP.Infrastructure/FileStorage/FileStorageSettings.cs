using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.FileStorage;

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    [Required]
    [MinLength(1)]
    public string RootPath { get; init; } = "./storage/files";

    [Range(1, 104857600)]
    public long MaxFileSizeBytes { get; init; } = 10_485_760;

    [Required]
    [MinLength(1)]
    public string[] AllowedContentTypes { get; init; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "text/csv",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ];
}
