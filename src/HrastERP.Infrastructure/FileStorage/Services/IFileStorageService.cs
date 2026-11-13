using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.FileStorage;

/// <summary>
/// Stores files on disk in a per-tenant folder and keeps their metadata as <see cref="StoredFile"/> records.
/// </summary>
public interface IFileStorageService
{
    Task<Result<FileUploadResult>> UploadAsync(
        Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default);

    /// <summary>The caller must dispose the returned stream.</summary>
    Task<Result<FileDownloadResult>> DownloadAsync(
        Guid fileId, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid fileId, CancellationToken ct = default);

    Task<Result<string>> GetUrlAsync(Guid fileId, CancellationToken ct = default);
}
