using FluentAssertions;
using HrastERP.Infrastructure.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.FileStorage;

public class FileStorageSettingsTests
{
    [Fact]
    public void SectionName_IsFileStorage()
    {
        FileStorageSettings.SectionName.Should().Be("FileStorage");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new FileStorageSettings();

        settings.RootPath.Should().Be("./storage/files");
        settings.MaxFileSizeBytes.Should().Be(10_485_760);
        settings.AllowedContentTypes.Should().HaveCount(8);
        settings.AllowedContentTypes.Should().Contain("application/pdf");
        settings.AllowedContentTypes.Should().Contain("image/jpeg");
    }

    [Fact]
    public void BindsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:RootPath"] = "/data/files",
                ["FileStorage:MaxFileSizeBytes"] = "5242880",
                ["FileStorage:AllowedContentTypes:0"] = "application/pdf",
                ["FileStorage:AllowedContentTypes:1"] = "image/png"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<FileStorageSettings>()
            .BindConfiguration(FileStorageSettings.SectionName);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<FileStorageSettings>>().Value;

        settings.RootPath.Should().Be("/data/files");
        settings.MaxFileSizeBytes.Should().Be(5_242_880);
        // Config binding for arrays is additive over defaults in .NET options
        settings.AllowedContentTypes.Should().Contain("application/pdf");
        settings.AllowedContentTypes.Should().Contain("image/png");
    }

    [Fact]
    public void MissingRootPath_FailsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:RootPath"] = "",
                ["FileStorage:AllowedContentTypes:0"] = "application/pdf"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<FileStorageSettings>()
            .BindConfiguration(FileStorageSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<FileStorageSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxFileSizeBytes_OutOfRange_FailsValidation(long size)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:RootPath"] = "/data/files",
                ["FileStorage:MaxFileSizeBytes"] = size.ToString(),
                ["FileStorage:AllowedContentTypes:0"] = "application/pdf"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<FileStorageSettings>()
            .BindConfiguration(FileStorageSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<FileStorageSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
