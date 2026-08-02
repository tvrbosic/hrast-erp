using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.FileStorage;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.FileStorage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Guid _tenantId = Guid.NewGuid();

    public LocalFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    private sealed class StubCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
    }

    private HrastDbContext CreateDbContext()
    {
        var tenant = new StubCurrentTenant(_tenantId);
        var options = new DbContextOptionsBuilder<HrastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new TenantEntityInterceptor(tenant))
            .Options;

        return new HrastDbContext(options, [], tenant);
    }

    private LocalFileStorageService CreateService(HrastDbContext dbContext, FileStorageSettings? settings = null)
    {
        settings ??= new FileStorageSettings { RootPath = _tempDir };
        return new LocalFileStorageService(
            Options.Create(settings),
            dbContext,
            new StubCurrentTenant(_tenantId),
            NullLogger<LocalFileStorageService>.Instance);
    }

    private static MemoryStream CreateTestStream(int sizeInBytes = 100)
    {
        var bytes = new byte[sizeInBytes];
        Random.Shared.NextBytes(bytes);
        return new MemoryStream(bytes);
    }

    [Fact]
    public async Task UploadAsync_ValidFile_SavesFileToDiskAndDatabase()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = CreateTestStream();

        var result = await service.UploadAsync(stream, "test.pdf", "application/pdf");

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("test.pdf");
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.SizeInBytes.Should().Be(100);

        var storedFile = await dbContext.Set<StoredFile>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == result.Value.FileId);
        storedFile.Should().NotBeNull();
        storedFile!.FileName.Should().Be("test.pdf");

        var fullPath = Path.Combine(_tempDir, storedFile.StoragePath);
        File.Exists(fullPath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_ExceedsMaxSize_ReturnsFileTooLarge()
    {
        await using var dbContext = CreateDbContext();
        var settings = new FileStorageSettings { RootPath = _tempDir, MaxFileSizeBytes = 50 };
        var service = CreateService(dbContext, settings);
        using var stream = CreateTestStream(100);

        var result = await service.UploadAsync(stream, "large.pdf", "application/pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.FileTooLarge);
    }

    [Fact]
    public async Task UploadAsync_DisallowedContentType_ReturnsContentTypeNotAllowed()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = CreateTestStream();

        var result = await service.UploadAsync(stream, "malware.exe", "application/x-msdownload");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.ContentTypeNotAllowed);
    }

    [Fact]
    public async Task DownloadAsync_ExistingFile_ReturnsStream()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var originalBytes = new byte[64];
        Random.Shared.NextBytes(originalBytes);
        using var uploadStream = new MemoryStream(originalBytes);

        var uploadResult = await service.UploadAsync(uploadStream, "doc.pdf", "application/pdf");

        var downloadResult = await service.DownloadAsync(uploadResult.Value.FileId);

        downloadResult.IsSuccess.Should().BeTrue();
        downloadResult.Value.FileName.Should().Be("doc.pdf");
        downloadResult.Value.ContentType.Should().Be("application/pdf");

        using var downloadedStream = downloadResult.Value.Stream;
        using var memoryStream = new MemoryStream();
        await downloadedStream.CopyToAsync(memoryStream);
        memoryStream.ToArray().Should().BeEquivalentTo(originalBytes);
    }

    [Fact]
    public async Task DownloadAsync_NonExistentId_ReturnsFileNotFound()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.DownloadAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.FileNotFound);
    }

    [Fact]
    public async Task DownloadAsync_DbRecordExistsButFileGone_ReturnsFileNotFound()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = CreateTestStream();

        var uploadResult = await service.UploadAsync(stream, "temp.pdf", "application/pdf");
        var storedFile = await dbContext.Set<StoredFile>()
            .IgnoreQueryFilters()
            .FirstAsync(f => f.Id == uploadResult.Value.FileId);

        // Delete the physical file manually
        File.Delete(Path.Combine(_tempDir, storedFile.StoragePath));

        var result = await service.DownloadAsync(uploadResult.Value.FileId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.FileNotFound);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesPhysicalFile()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = CreateTestStream();

        var uploadResult = await service.UploadAsync(stream, "delete-me.pdf", "application/pdf");
        var storedFile = await dbContext.Set<StoredFile>()
            .IgnoreQueryFilters()
            .FirstAsync(f => f.Id == uploadResult.Value.FileId);
        var fullPath = Path.Combine(_tempDir, storedFile.StoragePath);

        var result = await service.DeleteAsync(uploadResult.Value.FileId);

        result.IsSuccess.Should().BeTrue();
        File.Exists(fullPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFileNotFound()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.DeleteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.FileNotFound);
    }

    [Fact]
    public async Task GetUrlAsync_ExistingFile_ReturnsUrl()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = CreateTestStream();

        var uploadResult = await service.UploadAsync(stream, "url-test.pdf", "application/pdf");

        var result = await service.GetUrlAsync(uploadResult.Value.FileId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be($"/api/files/{uploadResult.Value.FileId}");
    }

    [Fact]
    public async Task GetUrlAsync_NonExistentId_ReturnsFileNotFound()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.GetUrlAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.FileNotFound);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
