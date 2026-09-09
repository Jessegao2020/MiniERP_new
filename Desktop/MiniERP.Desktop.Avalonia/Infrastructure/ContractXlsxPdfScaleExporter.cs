using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Produces the editable Contract workbook using the PDF-matched workbook layout,
/// then forces the OOXML worksheets to print at actual size. ClosedXML can leave
/// fit-to-page attributes behind after FitToPages was used by the base exporter;
/// LibreOffice/WPS may prefer those attributes and scale the whole page even when
/// Scale is later set to 100.
/// </summary>
public static class ContractXlsxPdfScaleExporter
{
    // Final horizontal tuning from side-by-side PDF/XLSX print preview.
    private const double ColumnWidthFactor = 0.625d;

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

            // Match the PDF's left/right printable margins. Keep the top margin
            // unchanged so the header stays aligned with the PDF; only reclaim a
            // little bottom space because WPS was moving the final page-number row
            // to a second, otherwise blank physical page.
            page.Margins.Left = 0.78d;
            page.Margins.Right = 0.78d;
            page.Margins.Top = 0.72d;
            page.Margins.Bottom = 0.05d;
            page.Margins.Header = 0d;
            page.Margins.Footer = 0d;
            page.CenterHorizontally = false;
            page.CenterVertically = false;
            page.ShowGridlines = false;

            // Explicit print area prevents WPS/Excel from extending printing into
            // styled-but-empty rows/columns and makes every worksheet map to exactly
            // one intended A4 contract page.
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            page.PrintAreas.Clear();
            page.PrintAreas.Add($"A1:H{lastRow}");

            sheet.SheetView.ZoomScale = 100;
            sheet.SheetView.ZoomScaleNormal = 100;
            sheet.SheetView.ZoomScalePageLayoutView = 100;
        }

        using var normalized = new MemoryStream();
        workbook.SaveAs(normalized);

        // ClosedXML's in-memory PageSetup can still serialize fitToWidth/fitToHeight
        // and sheetPr/pageSetUpPr fitToPage from the earlier FitToPages call. Remove
        // those OOXML flags after the final ClosedXML save so Excel/WPS/LibreOffice
        // must honor scale=100 rather than silently shrinking the page again.
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
