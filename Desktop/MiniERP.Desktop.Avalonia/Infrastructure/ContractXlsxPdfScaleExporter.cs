using ClosedXML.Excel;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Produces the editable Contract workbook using the PDF-matched workbook layout,
/// then removes spreadsheet print auto-scaling. The original worksheet geometry
/// was designed in PDF point sizes, but FitToPages(1, 1) caused LibreOffice/Excel
/// to print it at roughly 70% and therefore made every font and vertical distance
/// much smaller than ContractPdfExporter.
/// </summary>
public static class ContractXlsxPdfScaleExporter
{
    // Measured from the side-by-side print preview: the previous XLSX title was
    // about 70% of the PDF title height. Shrinking only the physical spreadsheet
    // columns by the same factor lets us print at 100% without changing the
    // existing horizontal footprint, while restoring PDF-sized fonts/row heights.
    private const double ColumnWidthFactor = 0.70d;

    public static void Export(Contract contract, Stream output)
    {
        using var intermediate = new MemoryStream();
        ContractXlsxExporter.Export(contract, intermediate);
        intermediate.Position = 0;

        using var workbook = new XLWorkbook(intermediate);
        foreach (var sheet in workbook.Worksheets)
        {
            for (var column = 1; column <= 8; column++)
                sheet.Column(column).Width *= ColumnWidthFactor;

            var page = sheet.PageSetup;
            page.PageOrientation = XLPageOrientation.Portrait;
            page.PaperSize = XLPaperSize.A4Paper;

            // Critical: do not use FitToPages here. It was shrinking the whole
            // worksheet (including fonts and row heights) to ~70% in print preview.
            page.AdjustTo(100);

            // Match the PDF's roughly 58 pt left/right printable margins while
            // keeping predictable results between Excel and LibreOffice.
            page.Margins.Left = 0.78d;
            page.Margins.Right = 0.78d;
            page.Margins.Top = 0.72d;
            page.Margins.Bottom = 0.25d;
            page.Margins.Header = 0d;
            page.Margins.Footer = 0d;
            page.CenterHorizontally = false;
            page.CenterVertically = false;
            page.ShowGridlines = false;

            sheet.SheetView.ZoomScale = 100;
            sheet.SheetView.ZoomScaleNormal = 100;
            sheet.SheetView.ZoomScalePageLayoutView = 100;
        }

        if (output.CanSeek)
        {
            output.Position = 0;
            output.SetLength(0);
        }

        workbook.SaveAs(output);
    }
}
