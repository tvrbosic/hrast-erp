using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Debugging;

namespace HrastERP.API.Tests.Logging;

// Reads the shipped appsettings files, which the API project copies into this test project's output folder.
public class SerilogConfigurationTests
{
    [Fact]
    public void Writes_to_overridden_file_path_without_errors_When_shipped_appsettings_are_loaded()
    {
        // Arrange: load both shipped files and override one File sink argument, as an environment variable would
        var tempDir = Path.Combine(Path.GetTempPath(), $"hrast-serilog-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:WriteTo:File:Args:path"] = Path.Combine(tempDir, "log-.log")
            })
            .Build();
        var selfLogMessages = new List<string>();

        try
        {
            // Act: build the logger from configuration and write one event.
            // Unknown sink or enricher names don't throw; Serilog reports them through SelfLog.
            SelfLog.Enable(message => selfLogMessages.Add(message));
            using (var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger())
            {
                logger.Information("Configuration test event");
            }

            // Assert: nothing was reported, and exactly one file was written at the overridden path
            selfLogMessages.Should().BeEmpty();
            Directory.Exists(tempDir).Should().BeTrue();
            Directory.GetFiles(tempDir).Should().ContainSingle();
        }
        finally
        {
            SelfLog.Disable();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
