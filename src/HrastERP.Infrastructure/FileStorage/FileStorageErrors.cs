using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.FileStorage;

public static class FileStorageErrors
{
    public static readonly Error FileNotFound =
        Error.NotFound("FileStorage.FileNotFound", "The requested file was not found.");

    public static readonly Error FileTooLarge =
        Error.Validation("FileStorage.FileTooLarge", "The file exceeds the maximum allowed size.");

    public static readonly Error ContentTypeNotAllowed =
        Error.Validation("FileStorage.ContentTypeNotAllowed", "The file type is not allowed.");

    public static readonly Error StorageFailed =
        Error.Unexpected("FileStorage.StorageFailed", "Failed to store the file.");
}
