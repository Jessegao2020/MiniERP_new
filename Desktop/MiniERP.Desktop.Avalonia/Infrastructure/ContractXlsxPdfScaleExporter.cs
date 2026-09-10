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
    private const double ColumnWidthFactor = 0.625d;
    private const double ContinuationHeaderGrowth = 24d;
    private const double FirstPageSpacingReduction = 38d;
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

            RemoveIntermediateItemRules(sheet);
            AlignFooterBankLabels(sheet);

            if (sheetIndex == 1)
                NormalizeFirstPageVerticalSpacing(sheet);

            if (sheetIndex > 1)
            {
                NormalizeContinuationHeader(sheet, contract);
                CompensateContinuationHeaderGrowth(sheet);
            }

            // Insert the same official logo used by the approved Quotation PDF only
            // after final column normalization, otherwise image anchoring can be
            // distorted when the workbook is rescaled for WPS/Excel printing.
            ApplyOfficialLogo(sheet);

            NormalizeFooterSeparatorPosition(sheet);

            var page = sheet.PageSetup;
            page.PageOrientation = XLPageOrientation.Portrait;
            page.PaperSize = XLPaperSize.A4Paper;
            page.AdjustTo(100);
            page.Margins.Left = 0.78d;
            page.Margins.Right = 0.78d;
            page.Margins.Top = 0.72d;
            page.Margins.Bottom = 0.05d;
            page.Margins.Header = 0d;
            page.Margins.Footer = 0d;
            page.CenterHorizontally = false;
            page.CenterVertically = false;
            page.ShowGridlines = false;

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            page.PrintAreas.Clear();
            page.PrintAreas.Add($"A1:H{lastRow}");

            sheet.SheetView.ZoomScale = 100;
            sheet.SheetView.ZoomScaleNormal = 100;
            sheet.SheetView.ZoomScalePageLayoutView = 100;
        }

        using var normalized = new MemoryStream();
        workbook.SaveAs(normalized);

        RemoveFitToPageFlags(normalized);
        normalized.Position = 0;

        if (output.CanSeek)
        {
            output.Position = 0;
            output.SetLength(0);
        }

        normalized.CopyTo(output);
    }

    private static void ApplyOfficialLogo(IXLWorksheet sheet)
    {
        sheet.Range("A1:C2").Clear(XLClearOptions.Contents);
        using var stream = new MemoryStream(DocumentBrandAssets.ForlinxLogoPng);
        var picture = sheet.AddPicture(stream);

        const int width = 132;
        var height = picture.OriginalWidth > 0
            ? Math.Max(1, (int)Math.Round(width * picture.OriginalHeight / (double)picture.OriginalWidth))
            : 32;
        picture.Width = width;
        picture.Height = height;
        // Slight downward offset mirrors the final Quotation PDF alignment where
        // the visible logo top is level with the company-name block.
        picture.MoveTo(sheet.Cell(1, 1), 0, 6);
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
        for (var row = 7; row <= 12; row++)
            sheet.Row(row).Height = 12;

        for (var row = 14; row <= 18; row++)
            sheet.Row(row).Height = 13;

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
        sheet.Range("D1:H1").Style.Font.FontSize = 10.2;
        sheet.Range("D2:H2").Style.Font.FontSize = 7.1;
        sheet.Row(1).Height = 22;
        sheet.Row(2).Height = 16;
        sheet.Row(3).Height = 8;

        var title = sheet.Range("A4:H4");
        title.Value = "Sales Contract";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 24;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(4).Height = 30;

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
                document = XDocument.Load(input, LoadOptions.PreserveWhitespace);

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
            document.Save(outputStream, SaveOptions.DisableFormatting);
        }
    }
}
