using FluentAssertions;
using HrastERP.Infrastructure.PdfGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.Infrastructure.Tests.PdfGeneration;

public class PdfGenerationSettingsTests
{
    [Fact]
    public void Has_correct_SectionName_When_class_is_defined()
    {
        PdfGenerationSettings.SectionName.Should().Be("PdfGeneration");
    }

    [Fact]
    public void Has_expected_default_values_When_instantiated()
    {
        var settings = new PdfGenerationSettings();

        settings.CompanyName.Should().Be("Hrast ERP");
        settings.LogoPath.Should().BeNull();
        settings.PrimaryColor.Should().Be("#2D5016");
        settings.AccentColor.Should().Be("#4A7C28");
        settings.TextColor.Should().Be("#1A1A1A");
        settings.FontFamily.Should().Be("Open Sans");
    }

    [Fact]
    public void Binds_all_properties_When_configuration_section_is_present()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfGeneration:CompanyName"] = "Test Company",
                ["PdfGeneration:LogoPath"] = "/path/to/logo.png",
                ["PdfGeneration:PrimaryColor"] = "#FF0000",
                ["PdfGeneration:AccentColor"] = "#00FF00",
                ["PdfGeneration:TextColor"] = "#0000FF",
                ["PdfGeneration:FontFamily"] = "Arial"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<PdfGenerationSettings>()
            .BindConfiguration(PdfGenerationSettings.SectionName);
        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<PdfGenerationSettings>>().Value;

        settings.CompanyName.Should().Be("Test Company");
        settings.LogoPath.Should().Be("/path/to/logo.png");
        settings.PrimaryColor.Should().Be("#FF0000");
        settings.AccentColor.Should().Be("#00FF00");
        settings.TextColor.Should().Be("#0000FF");
        settings.FontFamily.Should().Be("Arial");
    }

    [Theory]
    [InlineData("PdfGeneration:CompanyName")]
    [InlineData("PdfGeneration:PrimaryColor")]
    [InlineData("PdfGeneration:AccentColor")]
    [InlineData("PdfGeneration:TextColor")]
    [InlineData("PdfGeneration:FontFamily")]
    public void Fails_validation_When_required_field_is_missing(string fieldToOmit)
    {
        var allFields = new Dictionary<string, string?>
        {
            ["PdfGeneration:CompanyName"] = "Hrast ERP",
            ["PdfGeneration:PrimaryColor"] = "#2D5016",
            ["PdfGeneration:AccentColor"] = "#4A7C28",
            ["PdfGeneration:TextColor"] = "#1A1A1A",
            ["PdfGeneration:FontFamily"] = "Open Sans"
        };

        // Set the field to empty string to trigger [Required]/[MinLength] validation
        allFields[fieldToOmit] = "";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(allFields)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<PdfGenerationSettings>()
            .BindConfiguration(PdfGenerationSettings.SectionName)
            .ValidateDataAnnotations();
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<PdfGenerationSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
