using HrastERP.Infrastructure.Database;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.FileStorage;

internal sealed class LocalFileStorageService(
    IOptions<FileStorageSettings> settings,
    HrastDbContext dbContext,
    ICurrentTenant currentTenant,
    ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    private readonly FileStorageSettings _settings = settings.Value;

    /// <summary>
    /// Validates file size and content type, writes the file to disk under
    /// a tenant-isolated path ({TenantId}/{Year}/{Month}/{FileId}{Extension}),
    /// and persists a StoredFile record in the database.
    /// On failure, attempts best-effort cleanup of the partially written file.
    /// </summary>
    public async Task<Result<StoredFileInfo>> UploadAsync(
        Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default)
    {
        // Reject files that are too large or of a type that isn't allowed
        if (fileStream.Length > _settings.MaxFileSizeBytes)
            return FileStorageErrors.FileTooLarge;

        if (!_settings.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return FileStorageErrors.ContentTypeNotAllowed;

        // Build the tenant-isolated path; the file ID becomes the file name on disk
        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var now = DateTime.UtcNow;
        var storagePath = Path.Combine(
            currentTenant.TenantId.ToString(),
            now.Year.ToString(),
            now.Month.ToString("D2"),
            $"{fileId}{extension}");

        var fullPath = Path.Combine(_settings.RootPath, storagePath);

        // Write the file to disk, then save its StoredFile record
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var fileOnDisk = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(fileOnDisk, ct);

            var storedFile = new StoredFile(fileId, fileName, contentType, fileStream.Length, storagePath);
            dbContext.Set<StoredFile>().Add(storedFile);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("File uploaded: {FileId} ({FileName}, {ContentType}, {SizeInBytes} bytes)",
                fileId, fileName, contentType, fileStream.Length);

            return new StoredFileInfo
            {
                FileId = fileId,
                FileName = fileName,
                ContentType = contentType,
                SizeInBytes = fileStream.Length
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store file {FileName}", fileName);

            // Remove the file from disk so no file is left without a record
            try { File.Delete(fullPath); }
            catch { /* best-effort cleanup */ }

            return FileStorageErrors.StorageFailed;
        }
    }

    /// <summary>
    /// Looks up the StoredFile record by ID and opens a read-only stream to the
    /// physical file. Returns FileNotFound if the record or the physical file is missing.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    public async Task<Result<FileDownload>> DownloadAsync(
        Guid fileId, CancellationToken ct = default)
    {
        // Load the record; the tenant query filter hides other tenants' files
        var storedFile = await dbContext.Set<StoredFile>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);

        if (storedFile is null)
            return FileStorageErrors.FileNotFound;

        // Check that the file still exists on disk
        var fullPath = Path.Combine(_settings.RootPath, storedFile.StoragePath);

        if (!File.Exists(fullPath))
        {
            logger.LogWarning("Physical file missing for StoredFile {FileId} at {Path}", fileId, fullPath);
            return FileStorageErrors.FileNotFound;
        }

        // Open a read-only stream; the caller disposes it
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        return new FileDownload
        {
            Stream = stream,
            FileName = storedFile.FileName,
            ContentType = storedFile.ContentType
        };
    }

    /// <summary>
    /// Deletes the physical file from disk and removes the StoredFile record from
    /// the database (soft-delete via interceptor). If the physical file is already
    /// missing, the database record is still removed.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid fileId, CancellationToken ct = default)
    {
        // Load the record
        var storedFile = await dbContext.Set<StoredFile>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);

        if (storedFile is null)
            return Result.Failure(FileStorageErrors.FileNotFound);

        // Delete the file from disk; a failure is only logged so the record is still removed
        var fullPath = Path.Combine(_settings.RootPath, storedFile.StoragePath);

        try { File.Delete(fullPath); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete physical file at {Path}", fullPath);
        }

        // Remove the record (turned into a soft delete by SoftDeleteInterceptor)
        dbContext.Set<StoredFile>().Remove(storedFile);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("File deleted: {FileId} ({FileName})", fileId, storedFile.FileName);

        return Result.Success();
    }

    /// <summary>
    /// Returns a relative API URL (/api/files/{fileId}) for the given file.
    /// Intended for embedding download links in API responses or emails.
    /// The URL must be served by a controller endpoint that calls DownloadAsync.
    /// </summary>
    public async Task<Result<string>> GetUrlAsync(Guid fileId, CancellationToken ct = default)
    {
        var exists = await dbContext.Set<StoredFile>()
            .AnyAsync(f => f.Id == fileId, ct);

        if (!exists)
            return FileStorageErrors.FileNotFound;

        return $"/api/files/{fileId}";
    }
}
