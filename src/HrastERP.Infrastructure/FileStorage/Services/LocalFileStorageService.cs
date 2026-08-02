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

    public async Task<Result<FileUploadResult>> UploadAsync(
        Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default)
    {
        if (fileStream.Length > _settings.MaxFileSizeBytes)
            return FileStorageErrors.FileTooLarge;

        if (!_settings.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return FileStorageErrors.ContentTypeNotAllowed;

        var fileId = Guid.NewGuid();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var now = DateTime.UtcNow;
        var storagePath = Path.Combine(
            currentTenant.TenantId.ToString(),
            now.Year.ToString(),
            now.Month.ToString("D2"),
            $"{fileId}{extension}");

        var fullPath = Path.Combine(_settings.RootPath, storagePath);

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

            return new FileUploadResult
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

            try { File.Delete(fullPath); }
            catch { /* best-effort cleanup */ }

            return FileStorageErrors.StorageFailed;
        }
    }

    public async Task<Result<FileDownloadResult>> DownloadAsync(
        Guid fileId, CancellationToken ct = default)
    {
        var storedFile = await dbContext.Set<StoredFile>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);

        if (storedFile is null)
            return FileStorageErrors.FileNotFound;

        var fullPath = Path.Combine(_settings.RootPath, storedFile.StoragePath);

        if (!File.Exists(fullPath))
        {
            logger.LogWarning("Physical file missing for StoredFile {FileId} at {Path}", fileId, fullPath);
            return FileStorageErrors.FileNotFound;
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        return new FileDownloadResult
        {
            Stream = stream,
            FileName = storedFile.FileName,
            ContentType = storedFile.ContentType
        };
    }

    public async Task<Result> DeleteAsync(Guid fileId, CancellationToken ct = default)
    {
        var storedFile = await dbContext.Set<StoredFile>()
            .FirstOrDefaultAsync(f => f.Id == fileId, ct);

        if (storedFile is null)
            return Result.Failure(FileStorageErrors.FileNotFound);

        var fullPath = Path.Combine(_settings.RootPath, storedFile.StoragePath);

        try { File.Delete(fullPath); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete physical file at {Path}", fullPath);
        }

        dbContext.Set<StoredFile>().Remove(storedFile);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("File deleted: {FileId} ({FileName})", fileId, storedFile.FileName);

        return Result.Success();
    }

    public async Task<Result<string>> GetUrlAsync(Guid fileId, CancellationToken ct = default)
    {
        var exists = await dbContext.Set<StoredFile>()
            .AnyAsync(f => f.Id == fileId, ct);

        if (!exists)
            return FileStorageErrors.FileNotFound;

        return $"/api/files/{fileId}";
    }
}
