using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.FileStorage;

public interface IFileStorageService
{
    Task<Result<FileUploadResult>> UploadAsync(
        Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default);

    Task<Result<FileDownloadResult>> DownloadAsync(
        Guid fileId, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid fileId, CancellationToken ct = default);

    Task<Result<string>> GetUrlAsync(Guid fileId, CancellationToken ct = default);
}
