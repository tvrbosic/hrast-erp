# PDF Generation

QuestPDF-based PDF report generation with consistent branding. Each module implements `IReportGenerator<TData>` for its reports; the shared `ReportBuilder` service provides layout scaffolding (header, footer, page settings) and formatting helpers.

## Configuration

Settings in `appsettings.json` under `"PdfGeneration"`:

```json
{
  "PdfGeneration": {
    "CompanyName": "Hrast ERP",
    "LogoPath": null,
    "PrimaryColor": "#2D5016",
    "AccentColor": "#4A7C28",
    "TextColor": "#1A1A1A",
    "FontFamily": "Open Sans"
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `CompanyName` | string | `"Hrast ERP"` | Company name displayed in report headers (required) |
| `LogoPath` | string? | `null` | Path to a custom logo file. `null` uses the embedded default |
| `PrimaryColor` | string | `"#2D5016"` | Primary brand color (hex) for headers and dividers (required) |
| `AccentColor` | string | `"#4A7C28"` | Accent color (hex) for secondary elements (required) |
| `TextColor` | string | `"#1A1A1A"` | Default text color (hex) (required) |
| `FontFamily` | string | `"Open Sans"` | Font family name for report text (required) |

Bound to `PdfGenerationSettings` via `ValidateDataAnnotations()` + `ValidateOnStart()` — misconfiguration fails at startup.

## How It Works

Reports use a **composition pattern** — no base class inheritance:

1. **`ReportBuilder`** (singleton, DI-registered) provides shared layout: `ApplyDefaultPageSettings`, `ApplyHeader`, `ApplyFooter`, and formatting helpers like `FormatCurrency`, `FormatDate`.
2. **`IReportGenerator<TData>`** is the interface each module implements. It takes typed data and returns `Result<ReportResult>` with the PDF bytes, filename, and content type.
3. Module generators inject `ReportBuilder` and call its methods for the header/footer chrome, then fill in module-specific content in the page body.

## Report Layout

```
+--------------------------------------------------+
| [LOGO] Hrast ERP          Procurement Report     |  <- header
|                            Generated: 2026-08-31 |
| -------------------------------------------------|
|                                                  |
|  ... report content (module-specific) ...        |
|                                                  |
| -------------------------------------------------|
| 2026-08-31 14:30                    Page 1 of 3  |  <- footer
+--------------------------------------------------+
```

Defaults: A4 portrait, 2cm margins, Open Sans font, dark green branding.

## Writing a Report Generator

Each module creates an `IReportGenerator<TData>` implementation in its `Infrastructure/Reports/` folder.

```csharp
public sealed class ProcurementReportGenerator(
    ReportBuilder reportBuilder) : IReportGenerator<ProcurementReportData>
{
    public Result<ReportResult> Generate(ProcurementReportData data)
    {
        try
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    reportBuilder.ApplyDefaultPageSettings(page);
                    reportBuilder.ApplyHeader(page.Header(), "Procurement Report");

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Module-specific tables, summaries, data
                    });

                    reportBuilder.ApplyFooter(page.Footer());
                });
            });

            var bytes = document.GeneratePdf();

            return new ReportResult
            {
                Content = bytes,
                FileName = $"procurement-report-{DateTime.UtcNow:yyyyMMdd}.pdf",
                ContentType = "application/pdf"
            };
        }
        catch (Exception ex)
        {
            return PdfGenerationErrors.GenerationFailed;
        }
    }
}
```

Register in the module's DI method:

```csharp
services.AddScoped<IReportGenerator<ProcurementReportData>, ProcurementReportGenerator>();
```

## ReportBuilder Methods

### Layout

| Method | Signature | Description |
|---|---|---|
| `ApplyDefaultPageSettings` | `(PageDescriptor page)` | Sets A4, 2cm margins, default font and text color |
| `ApplyHeader` | `(IContainer container, string reportTitle)` | Logo + company name (left), report title + date (right), divider |
| `ApplyFooter` | `(IContainer container)` | Timestamp (left), Page X of Y (right), divider |

### Formatting

| Method | Signature | Description |
|---|---|---|
| `FormatCurrency` | `(decimal amount)` → `string` | Formats as `"N2"` (e.g., `"1,234.56"`) |
| `FormatDate` | `(DateTime date)` → `string` | ISO format `"yyyy-MM-dd"` |
| `PrimaryColor` | `()` → `string` | Returns configured primary color hex |
| `AccentColor` | `()` → `string` | Returns configured accent color hex |
| `TextColor` | `()` → `string` | Returns configured text color hex |

## Custom Branding

- **Logo:** Set `LogoPath` in settings to a file path for a custom logo. When `null`, the embedded default placeholder is used. Replace `src/HrastERP.Infrastructure/PdfGeneration/Assets/logo.png` with a real logo for the default.
- **Colors:** Override `PrimaryColor`, `AccentColor`, `TextColor` in settings per environment.
- **Company name:** Override `CompanyName` in settings per deployment.

## Error Codes

| Code | Type | When |
|---|---|---|
| `PdfGeneration.GenerationFailed` | Unexpected | QuestPDF throws during document generation |
| `PdfGeneration.LogoNotFound` | Unexpected | `LogoPath` points to a missing file |
