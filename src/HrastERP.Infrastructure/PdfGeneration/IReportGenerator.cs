using HrastERP.SharedKernel.Results;

namespace HrastERP.Infrastructure.PdfGeneration;

/// <summary>
/// Generates a PDF report from the given data.
/// Implemented by each module in its Infrastructure/Reports/ folder.
/// </summary>
public interface IReportGenerator<in TData>
{
    Result<ReportResult> Generate(TData data);
}
