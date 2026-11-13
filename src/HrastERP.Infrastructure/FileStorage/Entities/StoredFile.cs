using HrastERP.SharedKernel.Domain;

namespace HrastERP.Infrastructure.FileStorage;

/// <summary>
/// Metadata of a file stored on disk by <see cref="IFileStorageService"/>.
/// </summary>
public sealed class StoredFile : BaseEntity<Guid>, ITenantEntity
{
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeInBytes { get; private set; }
    /// <summary>Relative to <c>FileStorageSettings.RootPath</c>: <c>{TenantId}/{Year}/{Month}/{FileId}{extension}</c>.</summary>
    public string StoragePath { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }

    public StoredFile(Guid id, string fileName, string contentType, long sizeInBytes, string storagePath)
        : base(id)
    {
        FileName = fileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
        StoragePath = storagePath;
    }

    private StoredFile() { }
}
