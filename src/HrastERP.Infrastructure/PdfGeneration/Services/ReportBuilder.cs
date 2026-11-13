using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrastERP.Infrastructure.PdfGeneration;

/// <summary>
/// Shared layout helpers for the modules' <see cref="IReportGenerator{TData}"/> implementations: page
/// defaults, header, footer, formatting and the brand colors from <see cref="PdfGenerationSettings"/>.
/// Registered as a singleton.
/// </summary>
internal sealed class ReportBuilder(
    IOptions<PdfGenerationSettings> settings,
    ILogger<ReportBuilder> logger)
{
    private readonly PdfGenerationSettings _settings = settings.Value;
    private byte[]? _logoBytes;
    private bool _logoLoadAttempted;
    private readonly object _logoLock = new();

    /// <summary>
    /// Applies default page settings: A4 portrait, 2cm margins, default font and text color.
    /// </summary>
    public void ApplyDefaultPageSettings(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(2, Unit.Centimetre);
        page.DefaultTextStyle(style => style
            .FontSize(10)
            .FontFamily(_settings.FontFamily)
            .FontColor(_settings.TextColor));
    }

    /// <summary>
    /// Applies the standard report header: logo and company name on the left,
    /// report title and generation date on the right, with a divider line below.
    /// </summary>
    public void ApplyHeader(IContainer container, string reportTitle)
    {
        container.PaddingBottom(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                // Logo, if one is available, with a gap after it
                var logo = LoadLogo();
                if (logo is not null)
                {
                    row.ConstantItem(40).Image(logo).FitArea();
                    row.ConstantItem(10);
                }

                // Company name on the left
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_settings.CompanyName)
                        .Bold()
                        .FontSize(14)
                        .FontColor(_settings.PrimaryColor);
                });

                // Report title and generation date on the right
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignRight().Text(reportTitle)
                        .SemiBold()
                        .FontSize(12)
                        .FontColor(_settings.TextColor);
                    col.Item().AlignRight().Text($"Generated: {FormatDate(DateTime.UtcNow)}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            });

            // Divider below the header
            column.Item().PaddingTop(5).LineHorizontal(1).LineColor(_settings.PrimaryColor);
        });
    }

    /// <summary>
    /// Applies the standard report footer: generation timestamp on the left,
    /// page numbering on the right, with a divider line above.
    /// </summary>
    public void ApplyFooter(IContainer container)
    {
        container.PaddingTop(10).Column(column =>
        {
            // Divider above the footer
            column.Item().LineHorizontal(1).LineColor(_settings.PrimaryColor);

            column.Item().PaddingTop(5).Row(row =>
            {
                // Generation timestamp (UTC) on the left
                row.RelativeItem().Text(text =>
                {
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"))
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });

                // "Page X of Y" on the right
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    /// <summary>
    /// Formats a decimal amount as a currency string (e.g., "1,234.56").
    /// </summary>
    public string FormatCurrency(decimal amount)
    {
        return amount.ToString("N2");
    }

    /// <summary>
    /// Formats a DateTime as an ISO date string (e.g., "2026-08-31").
    /// </summary>
    public string FormatDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd");
    }

    /// <summary>Returns the configured primary color hex string for use in QuestPDF styling.</summary>
    public string PrimaryColor() => _settings.PrimaryColor;

    /// <summary>Returns the configured accent color hex string for use in QuestPDF styling.</summary>
    public string AccentColor() => _settings.AccentColor;

    /// <summary>Returns the configured text color hex string for use in QuestPDF styling.</summary>
    public string TextColor() => _settings.TextColor;

    private byte[]? LoadLogo()
    {
        // Load the logo only once (even when none is found); this singleton may render several
        // reports at the same time, so the lock with a second check stops a duplicate load
        if (_logoLoadAttempted)
            return _logoBytes;

        lock (_logoLock)
        {
            if (_logoLoadAttempted)
                return _logoBytes;

            _logoLoadAttempted = true;

            // Try custom path first
            if (!string.IsNullOrWhiteSpace(_settings.LogoPath))
            {
                if (File.Exists(_settings.LogoPath))
                {
                    _logoBytes = File.ReadAllBytes(_settings.LogoPath);
                    logger.LogInformation("Loaded report logo from {LogoPath}", _settings.LogoPath);
                    return _logoBytes;
                }

                logger.LogWarning("Configured logo path not found: {LogoPath}. Falling back to embedded logo",
                    _settings.LogoPath);
            }

            // Fall back to embedded resource
            var assembly = typeof(ReportBuilder).Assembly;
            var resourceName = "HrastERP.Infrastructure.PdfGeneration.Assets.logo.png";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                logger.LogWarning("Embedded logo resource not found: {ResourceName}. Reports will render without a logo",
                    resourceName);
                return null;
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            _logoBytes = memoryStream.ToArray();
            logger.LogInformation("Loaded embedded report logo");
            return _logoBytes;
        }
    }
}
