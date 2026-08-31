using System.ComponentModel.DataAnnotations;

namespace HrastERP.Infrastructure.PdfGeneration;

public sealed class PdfGenerationSettings
{
    public const string SectionName = "PdfGeneration";

    [Required]
    [MinLength(1)]
    public string CompanyName { get; init; } = "Hrast ERP";

    public string? LogoPath { get; init; }

    [Required]
    [MinLength(1)]
    public string PrimaryColor { get; init; } = "#2D5016";

    [Required]
    [MinLength(1)]
    public string AccentColor { get; init; } = "#4A7C28";

    [Required]
    [MinLength(1)]
    public string TextColor { get; init; } = "#1A1A1A";

    [Required]
    [MinLength(1)]
    public string FontFamily { get; init; } = "Open Sans";
}
