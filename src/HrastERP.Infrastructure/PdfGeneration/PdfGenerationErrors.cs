using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.PdfGeneration;

public static class PdfGenerationErrors
{
    public static readonly Error GenerationFailed =
        Error.Unexpected("PdfGeneration.GenerationFailed", "Failed to generate the PDF report.");

    public static readonly Error LogoNotFound =
        Error.Unexpected("PdfGeneration.LogoNotFound", "The configured logo file was not found.");
}
