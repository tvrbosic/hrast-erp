using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace HrastERP.Infrastructure.PdfGeneration;

internal static class PdfGenerationServiceExtensions
{
    /// <summary>
    /// Registers QuestPDF license, fonts, PDF generation settings, and the report builder service.
    /// Called internally by <see cref="InfrastructureServiceExtensions.AddInfrastructure"/>.
    /// </summary>
    internal static IServiceCollection AddPdfGenerationServices(this IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        RegisterEmbeddedFonts();

        services
            .AddOptions<PdfGenerationSettings>()
            .BindConfiguration(PdfGenerationSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ReportBuilder>();

        return services;
    }

    private static void RegisterEmbeddedFonts()
    {
        var assembly = typeof(PdfGenerationServiceExtensions).Assembly;

        RegisterFont(assembly, "HrastERP.Infrastructure.PdfGeneration.Assets.Fonts.OpenSans-Regular.ttf");
        RegisterFont(assembly, "HrastERP.Infrastructure.PdfGeneration.Assets.Fonts.OpenSans-Italic.ttf");
    }

    private static void RegisterFont(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
            FontManager.RegisterFont(stream);
    }
}
