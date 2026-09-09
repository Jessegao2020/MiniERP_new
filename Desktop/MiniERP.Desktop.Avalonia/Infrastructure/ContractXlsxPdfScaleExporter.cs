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

    // ContractXlsxExporter originally lays out continuation sheets using its compact
    // header. NormalizeContinuationHeader expands rows 1-5 from 66 pt to 90 pt.
    // The original bottom spacer was already calculated before that expansion, so
    // without compensating for these 24 pt the signature/footer shifts down and WPS
    // can push the last footer rows onto an otherwise blank physical page.
    private const double ContinuationHeaderGrowth = 24d;

    // The first-page party/address rows were 15 pt and the metadata rows were 17 pt,
    // while ContractPdfExporter advances those sections by 12 pt and 13 pt. Tighten
    // the XLSX to the same cadence, then put the reclaimed space back into the large
    // bottom spacer so the footer/signature remains anchored at the PDF position.
    private const double FirstPageSpacingReduction = 38d;

    // ContractXlsxExporter deliberately pads the page to this absolute height before
    // drawing the footer separator. Keep this invariant after all post-processing so
    // page 1, continuation pages and single-page contracts share the same footer Y.
    private const double FooterSeparatorTopHeight = 690d;

    public static void Export(Contract contract, Stream output)
    {
        using var intermediate = new MemoryStream();
        ContractXlsxExporter.Export(contract, intermediate);
        intermediate.Position = 0;

        using var workbook = new XLWorkbook(intermediate);
        for (var sheetIndex = 1; sheetIndex <= workbook.Worksheets.Count; sheetIndex++)
        {
            var sheet = workbook.Worksheet(sheetIndex);

            for (var column = 1; column <= 8; column++)
                sheet.Column(column).Width *= ColumnWidthFactor;

            // Match the PDF body: no separator rule between individual items,
            // only the table-header rule and one closing rule after the last item.
            RemoveIntermediateItemRules(sheet);

            // Keep bank-detail labels at the same visual start point as the PDF,
            // without sticking them directly against the value column.
            AlignFooterBankLabels(sheet);

            if (sheetIndex == 1)
                NormalizeFirstPageVerticalSpacing(sheet);

            // Every page uses the same full-size document header. The continuation
            // marker is separate and small; the brand/title itself is identical.
            if (sheetIndex > 1)
            {
                NormalizeContinuationHeader(sheet, contract);
                CompensateContinuationHeaderGrowth(sheet);
            }

            // Do this last: all prior header/body adjustments may change cumulative
            // row height. The footer separator itself must nevertheless remain at the
            // same absolute position on every worksheet, matching the PDF template.
            NormalizeFooterSeparatorPosition(sheet);

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

    private static void RemoveIntermediateItemRules(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var tableHeaderRow = 0;
        for (var row = 1; row <= lastRow; row++)
        {
            if (string.Equals(sheet.Cell(row, 1).GetString(), "Item", StringComparison.OrdinalIgnoreCase))
            {
                tableHeaderRow = row;
                break;
            }
        }

        if (tableHeaderRow == 0)
            return;

        var lastDetailRow = 0;
        for (var row = tableHeaderRow + 1; row <= lastRow; row++)
        {
            var itemText = sheet.Cell(row, 1).GetFormattedString().Trim();
            if (!int.TryParse(itemText, out _))
                continue;

            var detailRow = row + 1;
            if (detailRow > lastRow)
                break;

            sheet.Range(detailRow, 1, detailRow, 8).Style.Border.BottomBorder = XLBorderStyleValues.None;
            lastDetailRow = detailRow;
        }

        if (lastDetailRow > 0)
            sheet.Range(lastDetailRow, 1, lastDetailRow, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static void AlignFooterBankLabels(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 1; row <= lastRow; row++)
        {
            if (!string.Equals(sheet.Cell(row, 4).GetString(), "Bank Name:", StringComparison.OrdinalIgnoreCase))
                continue;

            for (var offset = 0; offset < 4 && row + offset <= lastRow; offset++)
            {
                var label = sheet.Range(row + offset, 4, row + offset, 5);
                label.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                label.Style.Alignment.Indent = 5;
            }
            break;
        }
    }

    private static void NormalizeFirstPageVerticalSpacing(IXLWorksheet sheet)
    {
        // PDF seller/buyer address lines advance by 12 pt.
        for (var row = 7; row <= 12; row++)
            sheet.Row(row).Height = 12;

        // PDF Contract No./Date/... metadata advances by 13 pt.
        for (var row = 14; row <= 18; row++)
            sheet.Row(row).Height = 13;

        // Keep the bottom-of-page elements at their existing absolute position.
        // On a single-page contract compensate before the signature block; on a
        // multi-page first sheet compensate before the footer separator. Do not add
        // the compensation to the separator row itself: that changes its height but
        // leaves its top border too high, which was the page-1 mismatch seen in WPS.
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var signatureRow = 0;
        for (var row = 1; row <= lastRow; row++)
        {
            if (string.Equals(sheet.Cell(row, 1).GetString(), "For Seller", StringComparison.OrdinalIgnoreCase))
            {
                signatureRow = row;
                break;
            }
        }

        if (signatureRow > 0)
        {
            AddBlankSpaceBefore(sheet, signatureRow, FirstPageSpacingReduction);
            return;
        }

        var footerRow = FindFooterCompanyRow(sheet);
        if (footerRow > 1)
            AddBlankSpaceBefore(sheet, footerRow - 1, FirstPageSpacingReduction);
    }

    private static void NormalizeContinuationHeader(IXLWorksheet sheet, Contract contract)
    {
        // Same brand geometry as page 1.
        sheet.Range("A1:C2").Style.Font.FontSize = 17;
        sheet.Range("D1:H1").Style.Font.FontSize = 10.2;
        sheet.Range("D2:H2").Style.Font.FontSize = 7;
        sheet.Row(1).Height = 22;
        sheet.Row(2).Height = 16;
        sheet.Row(3).Height = 8;

        // Same Sales Contract title as page 1.
        var title = sheet.Range("A4:H4");
        title.Value = "Sales Contract";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 22;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(4).Height = 30;

        // Keep the continuation identity without shrinking the actual header.
        var continued = sheet.Range("A5:H5");
        if (!continued.IsMerged())
            continued.Merge();
        continued.Value = $"{contract.ContractNumber} — continued";
        continued.Style.Font.FontSize = 7;
        continued.Style.Font.Italic = true;
        continued.Style.Font.FontColor = XLColor.Gray;
        continued.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        continued.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(5).Height = 14;
    }

    private static void CompensateContinuationHeaderGrowth(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        // On the final page, take the extra header height out of the large blank
        // spacer immediately before the signature block. This moves both signature
        // and footer back to the absolute Y positions calculated by the base layout.
        var signatureRow = 0;
        for (var row = 1; row <= lastRow; row++)
        {
            if (string.Equals(sheet.Cell(row, 1).GetString(), "For Seller", StringComparison.OrdinalIgnoreCase))
            {
                signatureRow = row;
                break;
            }
        }

        if (signatureRow > 0)
        {
            ReduceBlankSpaceBefore(sheet, signatureRow, ContinuationHeaderGrowth);
            return;
        }

        // Non-final continuation pages have no signature block. Compensate against
        // the footer spacer instead so their footer also remains on the intended A4.
        var footerRow = FindFooterCompanyRow(sheet);
        if (footerRow > 0)
            ReduceBlankSpaceBefore(sheet, footerRow, ContinuationHeaderGrowth);
    }

    private static int FindFooterCompanyRow(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var footerRow = 0;
        for (var row = 1; row <= lastRow; row++)
        {
            if (string.Equals(
                    sheet.Cell(row, 1).GetString(),
                    "Baoding Forlinx Embedded Technology Co., Ltd",
                    StringComparison.OrdinalIgnoreCase))
            {
                footerRow = row;
            }
        }
        return footerRow;
    }

    private static void NormalizeFooterSeparatorPosition(IXLWorksheet sheet)
    {
        var footerRow = FindFooterCompanyRow(sheet);
        if (footerRow <= 1)
            return;

        var separatorRow = footerRow - 1;

        // The separator row itself should remain the thin 4 pt rule row created by
        // ContractXlsxExporter. Any spacing compensation belongs before this row.
        sheet.Row(separatorRow).Height = 4d;

        var currentTop = 0d;
        for (var row = 1; row < separatorRow; row++)
            currentTop += sheet.Row(row).Height;

        var delta = FooterSeparatorTopHeight - currentTop;
        if (Math.Abs(delta) < 0.01d)
            return;

        if (delta > 0d)
            AddBlankSpaceBefore(sheet, separatorRow, delta);
        else
            ReduceBlankSpaceBefore(sheet, separatorRow, -delta);
    }

    private static void AddBlankSpaceBefore(IXLWorksheet sheet, int anchorRow, double amount)
    {
        for (var row = anchorRow - 1; row >= 1; row--)
        {
            var range = sheet.Range(row, 1, row, 8);
            var isBlank = range.Cells().All(cell => string.IsNullOrWhiteSpace(cell.GetString()));
            if (!isBlank)
                continue;

            sheet.Row(row).Height += amount;
            return;
        }
    }

    private static void ReduceBlankSpaceBefore(IXLWorksheet sheet, int anchorRow, double amount)
    {
        var remaining = amount;
        for (var row = anchorRow - 1; row >= 1 && remaining > 0.01d; row--)
        {
            var range = sheet.Range(row, 1, row, 8);
            var isBlank = range.Cells().All(cell => string.IsNullOrWhiteSpace(cell.GetString()));
            if (!isBlank)
                continue;

            var currentHeight = sheet.Row(row).Height;
            var reducible = Math.Max(0d, currentHeight - 4d);
            if (reducible <= 0d)
                continue;

            var reduction = Math.Min(reducible, remaining);
            sheet.Row(row).Height = currentHeight - reduction;
            remaining -= reduction;
        }
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
