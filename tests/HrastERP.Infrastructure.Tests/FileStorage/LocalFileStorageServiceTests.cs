using FluentAssertions;
using HrastERP.Infrastructure.Database;
using HrastERP.Infrastructure.FileStorage;
using HrastERP.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.FileStorage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ICurrentTenant _currentTenant;

    public LocalFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _currentTenant = Substitute.For<ICurrentTenant>();
        _currentTenant.TenantId.Returns(_tenantId);
    }

    private HrastDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HrastDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new TenantEntityInterceptor(_currentTenant))
            .Options;

        return new HrastDbContext(options, [], _currentTenant);
    }

    private LocalFileStorageService CreateService(HrastDbContext dbContext, FileStorageSettings? settings = null)
    {
        settings ??= new FileStorageSettings { RootPath = _tempDir };
        return new LocalFileStorageService(
            Options.Create(settings),
            dbContext,
            _currentTenant,
            NullLogger<LocalFileStorageService>.Instance);
    }

    private static MemoryStream CreateTestStream(int sizeInBytes = 100)
    {
        var bytes = new byte[sizeInBytes];
        Random.Shared.NextBytes(bytes);
        return new MemoryStream(bytes);
    }

    [Fact]
    public async Task Saves_file_to_disk_and_database_When_upload_succeeds()
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
    public async Task Returns_FileTooLarge_error_When_file_exceeds_max_size()
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
    public async Task Returns_ContentTypeNotAllowed_error_When_content_type_is_not_allowed()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = CreateTestStream();

        var result = await service.UploadAsync(stream, "malware.exe", "application/x-msdownload");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.ContentTypeNotAllowed);
    }

    [Fact]
    public async Task Returns_file_stream_When_file_exists()
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
    public async Task Returns_FileNotFound_error_When_file_id_does_not_exist()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.DownloadAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.FileNotFound);
    }

    [Fact]
    public async Task Returns_FileNotFound_error_When_physical_file_is_missing()
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
    public async Task Removes_physical_file_When_file_exists()
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
    public async Task Returns_FileNotFound_error_When_deleting_nonexistent_file()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.DeleteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FileStorageErrors.FileNotFound);
    }

    [Fact]
    public async Task Returns_file_url_When_file_exists()
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
    public async Task Returns_FileNotFound_error_When_getting_url_for_nonexistent_file()
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
