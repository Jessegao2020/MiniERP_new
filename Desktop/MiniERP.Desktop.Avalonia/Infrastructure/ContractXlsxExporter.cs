using ClosedXML.Excel;
using MiniERP.Domain;
using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

public static class ContractXlsxExporter
{
    // Keep these pagination numbers in sync with ContractPdfExporter. The Excel
    // workbook deliberately uses one worksheet per PDF page so the editable
    // version preserves the same document rhythm instead of letting Excel
    // squeeze every line item onto one automatically scaled page.
    private const float FirstPageItemsStartY = 350f;
    private const float ContinuationItemsStartY = 142f;
    private const float ItemsPageLimitY = 610f;
    private const double FooterTopHeight = 690d;
    private const double SignatureTopHeight = 620d;

    private static readonly SKTypeface DetailTypeface =
        SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Normal) ?? SKTypeface.Default;

    public static void Export(Contract contract, Stream output)
    {
        if (contract.Items.Count == 0)
            throw new InvalidOperationException("A contract needs at least one item before it can be exported.");

        var orderedItems = contract.Items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToList();
        var pages = BuildPages(orderedItems);

        using var workbook = new XLWorkbook();
        var pageAmountReferences = new List<string>();

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var pageNumber = pageIndex + 1;
            var page = pages[pageIndex];
            var sheet = workbook.Worksheets.Add($"Page {pageNumber}");
            ConfigurePage(sheet);

            var tableHeaderRow = pageIndex == 0
                ? BuildFirstPageHeader(sheet, contract)
                : BuildContinuationHeader(sheet, contract);

            BuildTableHeader(sheet, contract.Currency, tableHeaderRow);
            var itemsResult = BuildItems(
                sheet,
                page.Items,
                page.StartItemNumber,
                tableHeaderRow + 1,
                pageNumber);
            pageAmountReferences.AddRange(itemsResult.AmountReferences);

            var row = itemsResult.EndRow + 1;
            if (page.IsLast)
            {
                row = BuildClosingSections(sheet, contract, row, pageAmountReferences);
            }
            else
            {
                row = BuildContinuationNotice(sheet, row);
            }

            row = BuildFooter(sheet, row, pageNumber, pages.Count);

            // Each worksheet is one physical A4 page. This prevents the spreadsheet
            // application from compressing a two-page contract into one tiny page.
            sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
            sheet.PageSetup.FitToPages(1, 1);

            // Apply only the font family globally after layout. Font sizes are set
            // section-by-section and must not be overwritten here.
            var used = sheet.RangeUsed();
            if (used is not null)
                used.Style.Font.FontName = "DejaVu Sans";
        }

        workbook.SaveAs(output);
    }

    private static void ConfigurePage(IXLWorksheet sheet)
    {
        // A=Item, B:D=Product, E=Unit Price, F=Qty, G=Unit, H=Amount.
        // The proportions mirror the PDF's 478 pt printable-width layout.
        sheet.Column(1).Width = 6;
        sheet.Column(2).Width = 12;
        sheet.Column(3).Width = 16;
        sheet.Column(4).Width = 17;
        sheet.Column(5).Width = 15;
        sheet.Column(6).Width = 8;
        sheet.Column(7).Width = 8;
        sheet.Column(8).Width = 16;
    }

    private static int BuildFirstPageHeader(IXLWorksheet sheet, Contract contract)
    {
        BuildBrandHeader(sheet, compact: false);

        var title = sheet.Range("A4:H4").Merge();
        title.Value = "Sales Contract";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 22;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(4).Height = 30;
        sheet.Row(5).Height = 8;

        sheet.Range("A6:D6").Merge().Value = "SELLER";
        sheet.Range("E6:H6").Merge().Value = "BUYER";
        sheet.Range("A6:H6").Style.Font.Bold = true;
        sheet.Range("A6:H6").Style.Font.FontSize = 8.8;
        sheet.Row(6).Height = 18;

        sheet.Range("A7:D7").Merge().Value = "Baoding Forlinx Embedded Technology Co., Ltd";
        sheet.Range("A8:D8").Merge().Value = "2699 Xiangyang North Street";
        sheet.Range("A9:D9").Merge().Value = "071000 Baoding, China";

        sheet.Range("E7:H7").Merge().Value = contract.CustomerNameSnapshot ?? string.Empty;
        sheet.Range("E8:H8").Merge().Value = contract.CustomerContactSnapshot ?? string.Empty;

        var buyerAddress = SplitLines(contract.CustomerAddressSnapshot).Take(4).ToList();
        for (var index = 0; index < 4; index++)
            sheet.Range(9 + index, 5, 9 + index, 8).Merge().Value =
                index < buyerAddress.Count ? buyerAddress[index] : string.Empty;

        var parties = sheet.Range("A7:H12");
        parties.Style.Font.FontSize = 8;
        parties.Style.Alignment.WrapText = true;
        parties.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        for (var row = 7; row <= 12; row++)
            sheet.Row(row).Height = 15;

        sheet.Row(13).Height = 10;
        AddMetaRow(sheet, 14, "Contract No.", contract.ContractNumber,
            "Date", contract.ContractDate.ToString("MM.dd.yyyy"));
        AddMetaRow(sheet, 15, "Customer PO", contract.CustomerPoNumber ?? string.Empty,
            "PO Date", contract.CustomerPoDate?.ToString("MM.dd.yyyy") ?? string.Empty);
        AddMetaRow(sheet, 16, "Payment Term", contract.PaymentTerm ?? string.Empty,
            "Delivery Term", contract.DeliveryTerm ?? string.Empty);
        AddMetaRow(sheet, 17, "Lead Time", contract.LeadTime ?? string.Empty,
            "Contact", contract.SalesContactNameSnapshot ?? string.Empty);
        AddMetaRow(sheet, 18, "Phone", contract.SalesContactPhoneSnapshot ?? string.Empty,
            "Email", contract.SalesContactEmailSnapshot ?? string.Empty);

        var metadata = sheet.Range("A14:H18");
        metadata.Style.Font.FontSize = 8;
        metadata.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        metadata.Style.Alignment.WrapText = true;
        sheet.Range("A14:B18").Style.Font.Bold = true;
        sheet.Range("E14:F18").Style.Font.Bold = true;
        for (var row = 14; row <= 18; row++)
            sheet.Row(row).Height = 17;

        sheet.Row(19).Height = 8;
        return 20;
    }

    private static int BuildContinuationHeader(IXLWorksheet sheet, Contract contract)
    {
        BuildBrandHeader(sheet, compact: true);

        var continuation = sheet.Range("A4:H4").Merge();
        continuation.Value = $"Sales Contract {contract.ContractNumber} — continued";
        continuation.Style.Font.Bold = true;
        continuation.Style.Font.FontSize = 10;
        continuation.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(4).Height = 18;
        sheet.Row(5).Height = 8;
        return 6;
    }

    private static void BuildBrandHeader(IXLWorksheet sheet, bool compact)
    {
        var logo = sheet.Range("A1:C2").Merge();
        logo.Value = "◀◀◀◀ FORLINX";
        logo.Style.Font.Bold = true;
        logo.Style.Font.FontSize = compact ? 14 : 17;
        logo.Style.Font.FontColor = XLColor.Blue;
        logo.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var company = sheet.Range("D1:H1").Merge();
        company.Value = "Baoding Forlinx Embedded Technology Co., Ltd";
        company.Style.Font.Bold = true;
        company.Style.Font.FontSize = compact ? 8.6 : 10.2;
        company.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        company.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

        var tagline = sheet.Range("D2:H2").Merge();
        tagline.Value = "Trusted Designer & Manufacturer of System on Module";
        tagline.Style.Font.Italic = true;
        tagline.Style.Font.FontSize = 7;
        tagline.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        tagline.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        sheet.Row(1).Height = compact ? 18 : 22;
        sheet.Row(2).Height = compact ? 14 : 16;
        sheet.Range("A3:H3").Style.Border.TopBorder = XLBorderStyleValues.Thin;
        sheet.Row(3).Height = 8;
    }

    private static void AddMetaRow(
        IXLWorksheet sheet,
        int row,
        string leftLabel,
        string leftValue,
        string rightLabel,
        string rightValue)
    {
        sheet.Range(row, 1, row, 2).Merge().Value = leftLabel;
        sheet.Range(row, 3, row, 4).Merge().Value = leftValue;
        sheet.Range(row, 5, row, 6).Merge().Value = rightLabel;
        sheet.Range(row, 7, row, 8).Merge().Value = rightValue;
    }

    private static void BuildTableHeader(IXLWorksheet sheet, string currency, int row)
    {
        sheet.Cell(row, 1).Value = "Item";
        sheet.Range(row, 2, row, 4).Merge().Value = "Product";
        sheet.Cell(row, 5).Value = $"Unit Price ({currency})";
        sheet.Cell(row, 6).Value = "Qty";
        sheet.Cell(row, 7).Value = "Unit";
        sheet.Cell(row, 8).Value = "Amount";

        var header = sheet.Range(row, 1, row, 8);
        header.Style.Font.Bold = true;
        header.Style.Font.FontSize = 8.5;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        sheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Range(row, 2, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Row(row).Height = 23;
    }

    private static PageBuildResult BuildItems(
        IXLWorksheet sheet,
        IReadOnlyList<ContractItem> items,
        int startItemNumber,
        int startRow,
        int pageNumber)
    {
        var row = startRow;
        var itemNo = startItemNumber;
        var amountReferences = new List<string>();

        foreach (var item in items)
        {
            var mainRow = row;
            var detailRow = row + 1;
            var itemHeight = MeasureItemHeight(item);
            var detailHeight = Math.Max(25d, itemHeight - 18d);
            var unit = string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit;
            var netPrice = decimal.Round(
                item.UnitPrice * (1m - item.DiscountPercent / 100m),
                2,
                MidpointRounding.AwayFromZero);

            sheet.Range(mainRow, 1, detailRow, 1).Merge().Value = itemNo++;
            sheet.Range(mainRow, 2, mainRow, 4).Merge().Value = item.ArticleName;
            sheet.Range(detailRow, 2, detailRow, 4).Merge().Value = item.Description ?? string.Empty;
            sheet.Range(mainRow, 5, detailRow, 5).Merge().Value = netPrice;
            sheet.Range(mainRow, 6, detailRow, 6).Merge().Value = item.Quantity;
            sheet.Range(mainRow, 7, detailRow, 7).Merge().Value = unit;
            sheet.Range(mainRow, 8, detailRow, 8).Merge();
            sheet.Cell(mainRow, 8).FormulaA1 = $"=ROUND(E{mainRow}*F{mainRow},2)";

            var body = sheet.Range(mainRow, 1, detailRow, 8);
            body.Style.Font.FontSize = 8.2;
            body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.Range(mainRow, 2, mainRow, 4).Style.Font.Bold = true;
            sheet.Range(mainRow, 2, mainRow, 4).Style.Font.FontSize = 8.8;
            sheet.Range(detailRow, 2, detailRow, 4).Style.Font.FontSize = 7.1;
            sheet.Range(detailRow, 2, detailRow, 4).Style.Alignment.WrapText = true;

            sheet.Range(mainRow, 1, detailRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(mainRow, 5, detailRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Range(mainRow, 7, detailRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheet.Range(mainRow, 8, detailRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            sheet.Cell(mainRow, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(mainRow, 6).Style.NumberFormat.Format = "0.####";
            sheet.Cell(mainRow, 8).Style.NumberFormat.Format = "#,##0.00";

            sheet.Row(mainRow).Height = 18;
            sheet.Row(detailRow).Height = detailHeight;
            sheet.Range(detailRow, 1, detailRow, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            amountReferences.Add($"'Page {pageNumber}'!H{mainRow}");
            row += 2;
        }

        return new PageBuildResult(row - 1, amountReferences);
    }

    private static int BuildContinuationNotice(IXLWorksheet sheet, int row)
    {
        sheet.Row(row).Height = 18;
        var notice = sheet.Range(row, 1, row, 8).Merge();
        notice.Value = "Continued on next page";
        notice.Style.Font.Italic = true;
        notice.Style.Font.FontSize = 7;
        notice.Style.Font.FontColor = XLColor.Gray;
        notice.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        return row + 1;
    }

    private static int BuildClosingSections(
        IXLWorksheet sheet,
        Contract contract,
        int row,
        IReadOnlyList<string> amountReferences)
    {
        sheet.Row(row).Height = 12;
        row++;

        var totalRow = row;
        sheet.Range(totalRow, 5, totalRow, 7).Merge().Value = "Total";
        sheet.Range(totalRow, 5, totalRow, 7).Style.Font.Bold = true;
        sheet.Range(totalRow, 5, totalRow, 7).Style.Font.FontSize = 8.5;
        sheet.Range(totalRow, 5, totalRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(totalRow, 8).FormulaA1 = $"=SUM({string.Join(",", amountReferences)})";
        sheet.Cell(totalRow, 8).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(totalRow, 8).Style.Font.Bold = true;
        sheet.Cell(totalRow, 8).Style.Font.FontSize = 8.5;
        sheet.Cell(totalRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Range(totalRow, 5, totalRow, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        sheet.Row(totalRow).Height = 20;
        row++;

        sheet.Row(row).Height = 16;
        row++;

        var terms = BuildTerms(contract);
        if (terms.Count > 0)
        {
            sheet.Range(row, 1, row, 8).Merge().Value = "Terms and Conditions";
            sheet.Range(row, 1, row, 8).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 8).Style.Font.FontSize = 8.5;
            sheet.Row(row).Height = 18;
            row++;

            foreach (var paragraph in terms)
            {
                var lines = CountVisualLines(paragraph, 105);
                var range = sheet.Range(row, 1, row, 8).Merge();
                range.Value = paragraph;
                range.Style.Font.FontSize = 7.6;
                range.Style.Alignment.WrapText = true;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                sheet.Row(row).Height = Math.Max(16, 8 + lines * 10);
                row++;
            }
        }

        row = PadToHeight(sheet, row, SignatureTopHeight);

        sheet.Range(row, 1, row, 4).Merge().Value = "For Seller";
        sheet.Range(row, 5, row, 8).Merge().Value = "For Buyer";
        sheet.Range(row, 1, row, 8).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 8).Style.Font.FontSize = 8.5;
        sheet.Row(row).Height = 18;
        row++;

        // Same 35 pt signature/stamp space as the PDF before the signature rule.
        sheet.Row(row).Height = 34;
        row++;

        sheet.Range(row, 1, row, 4).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 5, row, 8).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 1, row, 4).Merge().Value = "Authorized Signature / Stamp";
        sheet.Range(row, 5, row, 8).Merge().Value = "Authorized Signature / Stamp";
        sheet.Range(row, 1, row, 8).Style.Font.FontSize = 7.6;
        sheet.Row(row).Height = 16;
        return row + 1;
    }

    private static List<string> BuildTerms(Contract contract)
    {
        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(contract.PaymentTerm))
            terms.Add($"Payment Term: {contract.PaymentTerm}");
        if (!string.IsNullOrWhiteSpace(contract.DeliveryTerm))
            terms.Add($"Delivery Term: {contract.DeliveryTerm}");
        if (!string.IsNullOrWhiteSpace(contract.LeadTime))
            terms.Add($"Lead Time: {contract.LeadTime}");
        if (!string.IsNullOrWhiteSpace(contract.BankInformation))
            terms.Add($"Bank Information: {contract.BankInformation}");
        if (!string.IsNullOrWhiteSpace(contract.Remarks))
            terms.Add($"Remarks: {contract.Remarks}");
        if (!string.IsNullOrWhiteSpace(contract.TermsAndConditions))
            terms.Add(contract.TermsAndConditions.Trim());
        return terms;
    }

    private static int BuildFooter(IXLWorksheet sheet, int row, int pageNumber, int pageCount)
    {
        row = PadToHeight(sheet, row, FooterTopHeight);

        sheet.Range(row, 1, row, 8).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        sheet.Row(row).Height = 4;
        row++;

        var leftLines = new[]
        {
            "Baoding Forlinx Embedded Technology Co., Ltd",
            "2699 Xiangyang North Street",
            "071000 Baoding",
            "China"
        };
        var labels = new[] { "Bank Name:", "Bank Address:", "Bank Account:", "Swift Code:" };
        var values = new[]
        {
            "China Construction Bank",
            "345 Longxing West Rd, Baoding, China",
            "1301 4600 6002 2010 0241",
            "PCBCCNBJ"
        };

        var footerStart = row;
        for (var index = 0; index < 4; index++)
        {
            sheet.Range(row + index, 1, row + index, 3).Merge().Value = leftLines[index];
            sheet.Range(row + index, 4, row + index, 5).Merge().Value = labels[index];
            sheet.Range(row + index, 6, row + index, 8).Merge().Value = values[index];
            sheet.Range(row + index, 4, row + index, 5).Style.Font.Bold = true;
            sheet.Row(row + index).Height = 10.5;
        }

        var footer = sheet.Range(footerStart, 1, footerStart + 3, 8);
        footer.Style.Font.FontSize = 6.4;
        footer.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(footerStart, 1, footerStart, 3).Style.Font.Bold = true;
        row = footerStart + 4;

        var page = sheet.Range(row, 1, row, 8).Merge();
        page.Value = $"Page {pageNumber} of {pageCount}";
        page.Style.Font.FontSize = 8;
        page.Style.Font.FontColor = XLColor.Gray;
        page.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        page.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(row).Height = 20;
        return row;
    }

    private static int PadToHeight(IXLWorksheet sheet, int row, double targetHeight)
    {
        var currentHeight = 0d;
        for (var current = 1; current < row; current++)
            currentHeight += sheet.Row(current).Height;

        if (currentHeight >= targetHeight)
            return row;

        sheet.Row(row).Height = targetHeight - currentHeight;
        return row + 1;
    }

    private static List<PagePlan> BuildPages(IReadOnlyList<ContractItem> items)
    {
        var pages = new List<PagePlan>();
        var current = new List<ContractItem>();
        var y = FirstPageItemsStartY;
        var startNo = 1;
        var currentStart = 1;

        foreach (var item in items)
        {
            var height = MeasureItemHeight(item);
            if (current.Count > 0 && y + height > ItemsPageLimitY)
            {
                pages.Add(new PagePlan(currentStart, current.ToList(), false));
                startNo += current.Count;
                currentStart = startNo;
                current.Clear();
                y = ContinuationItemsStartY;
            }

            current.Add(item);
            y += height;
        }

        pages.Add(new PagePlan(currentStart, current.ToList(), true));
        return pages;
    }

    private static float MeasureItemHeight(ContractItem item)
    {
        using var detail = new SKPaint
        {
            Typeface = DetailTypeface,
            TextSize = 7.1f,
            IsAntialias = true
        };
        var lines = WrapText(item.Description, 220f, detail);
        return Math.Max(43f, 28f + lines.Count * 10f);
    }

    private static List<string> WrapText(string? value, float width, SKPaint paint)
    {
        var result = new List<string>();
        foreach (var paragraph in SplitLinesIncludingEmpty(value))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                result.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (paint.MeasureText(candidate) <= width)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                    result.Add(current);
                current = word;
            }

            if (!string.IsNullOrEmpty(current))
                result.Add(current);
        }

        return result;
    }

    private static IEnumerable<string> SplitLines(string? value)
        => SplitLinesIncludingEmpty(value)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim());

    private static IEnumerable<string> SplitLinesIncludingEmpty(string? value)
        => (value ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

    private static int CountVisualLines(string? value, int charactersPerLine)
    {
        if (string.IsNullOrWhiteSpace(value)) return 1;
        var normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        var count = 0;
        foreach (var line in normalized.Split('\n'))
            count += Math.Max(1, (int)Math.Ceiling(line.Length / (double)charactersPerLine));
        return Math.Clamp(count, 1, 12);
    }

    private sealed record PagePlan(int StartItemNumber, List<ContractItem> Items, bool IsLast);
    private sealed record PageBuildResult(int EndRow, List<string> AmountReferences);
}
