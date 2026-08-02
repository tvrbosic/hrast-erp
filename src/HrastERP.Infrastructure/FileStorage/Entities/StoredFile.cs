using HrastERP.SharedKernel.Domain;

namespace HrastERP.Infrastructure.FileStorage;

public sealed class StoredFile : BaseEntity<Guid>, ITenantEntity
{
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeInBytes { get; private set; }
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
