using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Produces the editable Contract workbook using the PDF-matched workbook layout,
/// then forces the OOXML worksheets to print at actual size. ClosedXML can leave
/// fit-to-page attributes behind after FitToPages was used by the base exporter;
/// LibreOffice prefers those attributes and scales the whole page even when Scale
/// is later set to 100.
/// </summary>
public static class ContractXlsxPdfScaleExporter
{
    // The user's print-preview comparison showed the residual fit-to-page scale was
    // about 70%. At 100% print scale the columns therefore need the two accumulated
    // 0.70 factors (0.70 * 0.70 ~= 0.49) to retain the same PDF-like printable width.
    // Font sizes and row heights are intentionally NOT reduced.
    private const double ColumnWidthFactor = 0.49d;

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
            page.AdjustTo(100);

            // Match the PDF's roughly 58 pt left/right margins.
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

        using var normalized = new MemoryStream();
        workbook.SaveAs(normalized);

        // ClosedXML's in-memory PageSetup can still serialize fitToWidth/fitToHeight
        // and sheetPr/pageSetUpPr fitToPage from the earlier FitToPages call. Remove
        // those OOXML flags after the final ClosedXML save so Excel/LibreOffice must
        // honor scale=100 rather than silently shrinking the page again.
        RemoveFitToPageFlags(normalized);
        normalized.Position = 0;

        if (output.CanSeek)
        {
            output.Position = 0;
            output.SetLength(0);
        }

        normalized.CopyTo(output);
    }

    private static void RemoveFitToPageFlags(MemoryStream packageStream)
    {
        packageStream.Position = 0;
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true);
        var sheetNames = archive.Entries
            .Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)
                            && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .ToList();

        foreach (var sheetName in sheetNames)
        {
            var entry = archive.GetEntry(sheetName);
            if (entry is null) continue;

            XDocument document;
            using (var input = entry.Open())
                document = XDocument.Load(input, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var root = document.Root;
            if (root is null) continue;
            var ns = root.Name.Namespace;

            var pageSetup = root.Element(ns + "pageSetup");
            if (pageSetup is not null)
            {
                pageSetup.SetAttributeValue("scale", "100");
                pageSetup.Attribute("fitToWidth")?.Remove();
                pageSetup.Attribute("fitToHeight")?.Remove();
            }

            var pageSetupProperties = root
                .Element(ns + "sheetPr")?
                .Element(ns + "pageSetUpPr");
            pageSetupProperties?.Attribute("fitToPage")?.Remove();

            entry.Delete();
            var replacement = archive.CreateEntry(sheetName, CompressionLevel.Optimal);
            using var outputStream = replacement.Open();
            document.Save(outputStream, System.Xml.Linq.SaveOptions.DisableFormatting);
        }
    }
}
