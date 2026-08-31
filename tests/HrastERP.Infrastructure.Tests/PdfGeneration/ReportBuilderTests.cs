using FluentAssertions;
using HrastERP.Infrastructure.PdfGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrastERP.Infrastructure.Tests.PdfGeneration;

public class ReportBuilderTests
{
    private readonly ReportBuilder _sut;

    static ReportBuilderTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportBuilderTests()
    {
        var settings = Options.Create(new PdfGenerationSettings());
        _sut = new ReportBuilder(settings, NullLogger<ReportBuilder>.Instance);
    }

    [Fact]
    public void FormatCurrency_PositiveAmount_ReturnsFormattedString()
    {
        var result = _sut.FormatCurrency(1234.56m);

        result.Should().Contain("1").And.Contain("234").And.Contain("56");
    }

    [Fact]
    public void FormatCurrency_Zero_ReturnsFormattedZero()
    {
        var result = _sut.FormatCurrency(0m);

        result.Should().Contain("0").And.Contain("00");
    }

    [Fact]
    public void FormatCurrency_NegativeAmount_ReturnsFormattedNegative()
    {
        var result = _sut.FormatCurrency(-500.00m);

        result.Should().Contain("500").And.Contain("00");
    }

    [Fact]
    public void FormatDate_ReturnsIsoFormat()
    {
        var result = _sut.FormatDate(new DateTime(2026, 8, 31));

        result.Should().Be("2026-08-31");
    }

    [Fact]
    public void PrimaryColor_ReturnsConfiguredHex()
    {
        _sut.PrimaryColor().Should().Be("#2D5016");
    }

    [Fact]
    public void AccentColor_ReturnsConfiguredHex()
    {
        _sut.AccentColor().Should().Be("#4A7C28");
    }

    [Fact]
    public void TextColor_ReturnsConfiguredHex()
    {
        _sut.TextColor().Should().Be("#1A1A1A");
    }

    [Fact]
    public void Generate_WithReportBuilder_ProducesValidPdf()
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                _sut.ApplyDefaultPageSettings(page);
                _sut.ApplyHeader(page.Header(), "Test Report");

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text("Test content");
                });

                _sut.ApplyFooter(page.Footer());
            });
        });

        var bytes = document.GeneratePdf();

        bytes.Should().NotBeEmpty();
        // PDF magic bytes: %PDF
        bytes[0].Should().Be(0x25); // %
        bytes[1].Should().Be(0x50); // P
        bytes[2].Should().Be(0x44); // D
        bytes[3].Should().Be(0x46); // F
    }
}
