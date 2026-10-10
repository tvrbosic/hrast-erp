# File Storage

Local file storage with tenant-isolated directory structure. Files are stored on disk and tracked in the `stored_files` database table.

## Configuration

Settings in `appsettings.json` under `"FileStorage"`:

```json
{
  "FileStorage": {
    "RootPath": "./storage/files",
    "MaxFileSizeBytes": 10485760,
    "AllowedContentTypes": [
      "application/pdf",
      "image/jpeg",
      "image/png",
      "image/gif",
      "image/webp",
      "text/csv",
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ]
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `RootPath` | string | `./storage/files` | Root directory for file storage (required) |
| `MaxFileSizeBytes` | long (1–104857600) | 10485760 (10 MB) | Maximum allowed file size |
| `AllowedContentTypes` | string[] | PDF, images, CSV, XLSX, DOCX | Permitted MIME types (required, at least one) |

Bound to `FileStorageSettings` via `ValidateDataAnnotations()` + `ValidateOnStart()` — misconfiguration fails at startup.

## Storage Layout

Files are organized by tenant and date:

```
storage/files/
└── {TenantId}/
    └── {Year}/
        └── {Month}/
            └── {FileId}{Extension}
```

The `storage/` directory is git-ignored.

## Using the Service

Inject `IFileStorageService` into your handler. All methods return `Result<T>` or `Result`.

### Upload

```csharp
internal sealed class UploadDocumentHandler(IFileStorageService fileStorage)
    : IRequestHandler<UploadDocumentCommand, Result<FileUploadResult>>
{
    public async Task<Result<FileUploadResult>> Handle(
        UploadDocumentCommand command, CancellationToken ct)
    {
        return await fileStorage.UploadAsync(
            command.FileStream, command.FileName, command.ContentType, ct);
    }
}
```

`UploadAsync` validates file size and content type, writes the file to disk, creates a `StoredFile` entity, and returns a `FileUploadResult` with the assigned `FileId`.

### Download

```csharp
var result = await fileStorage.DownloadAsync(fileId, ct);
if (result.IsSuccess)
{
    // result.Value.Stream — the file stream (caller must dispose)
    // result.Value.FileName — original file name
    // result.Value.ContentType — MIME type
}
```

Returns `FileStorageErrors.FileNotFound` if the record or physical file is missing.

### Delete

```csharp
var result = await fileStorage.DeleteAsync(fileId, ct);
```

Removes both the physical file and the database record. Uses soft-delete via the interceptor chain.

### Get URL

```csharp
var result = await fileStorage.GetUrlAsync(fileId, ct);
// result.Value is "/api/files/{fileId}"
```

Returns a relative URL for the file. Useful for building download links.

## StoredFile Entity

`StoredFile` extends `BaseEntity<Guid>` and implements `ITenantEntity`. It gets automatic auditing, soft-delete, and tenant isolation.

| Column | Type | Description |
|---|---|---|
| `Id` | uuid | Primary key |
| `FileName` | varchar(256) | Original file name |
| `ContentType` | varchar(128) | MIME type |
| `SizeInBytes` | bigint | File size |
| `StoragePath` | varchar(512) | Relative path within `RootPath` (unique index) |
| `TenantId` | uuid | Tenant isolation (indexed) |
| Audit fields | — | `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt`, `DeletedBy` |

## Error Codes

| Error | Type | When |
|---|---|---|
| `FileStorage.FileNotFound` | NotFound | File ID doesn't exist or physical file is missing |
| `FileStorage.FileTooLarge` | Validation | File exceeds `MaxFileSizeBytes` |
| `FileStorage.ContentTypeNotAllowed` | Validation | MIME type not in `AllowedContentTypes` |
| `FileStorage.StorageFailed` | Unexpected | Disk write or database save failed |
