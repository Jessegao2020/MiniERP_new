using ClosedXML.Excel;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

public static class ContractXlsxExporter
{
    public static void Export(Contract contract, Stream output)
    {
        if (contract.Items.Count == 0)
            throw new InvalidOperationException("A contract needs at least one item before it can be exported.");

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Contract");

        ConfigureColumns(sheet);
        BuildHeader(sheet, contract);
        var itemEndRow = BuildItems(sheet, contract);
        var contentEndRow = BuildTermsAndSignatures(sheet, contract, itemEndRow + 2);

        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);

        sheet.Range(1, 1, contentEndRow, 8).Style.Font.FontSize = 10;
        workbook.SaveAs(output);
    }

    private static void ConfigureColumns(IXLWorksheet sheet)
    {
        sheet.Column(1).Width = 5.5;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 31;
        sheet.Column(4).Width = 10;
        sheet.Column(5).Width = 9;
        sheet.Column(6).Width = 14;
        sheet.Column(7).Width = 10;
        sheet.Column(8).Width = 16;
    }

    private static void BuildHeader(IXLWorksheet sheet, Contract contract)
    {
        var company = sheet.Range("A1:H1").Merge();
        company.Value = "Baoding Forlinx Embedded Technology Co., Ltd";
        company.Style.Font.Bold = true;
        company.Style.Font.FontSize = 15;
        company.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Row(1).Height = 24;

        var title = sheet.Range("A2:H2").Merge();
        title.Value = "SALES CONTRACT";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 18;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Row(2).Height = 28;

        sheet.Range("A4:D4").Merge().Value = "SELLER";
        sheet.Range("E4:H4").Merge().Value = "BUYER";
        StyleSectionHeader(sheet.Range("A4:D4"));
        StyleSectionHeader(sheet.Range("E4:H4"));

        sheet.Range("A5:D5").Merge().Value = "Baoding Forlinx Embedded Technology Co., Ltd";
        sheet.Range("A6:D8").Merge().Value = "2699 Xiangyang North Street\n071000 Baoding\nChina";
        sheet.Range("A9:D9").Merge().Value = FormatContact(
            contract.SalesContactNameSnapshot,
            contract.SalesContactPhoneSnapshot,
            contract.SalesContactEmailSnapshot);

        sheet.Range("E5:H5").Merge().Value = contract.CustomerNameSnapshot ?? string.Empty;
        sheet.Range("E6:H8").Merge().Value = contract.CustomerAddressSnapshot ?? string.Empty;
        sheet.Range("E9:H9").Merge().Value = string.IsNullOrWhiteSpace(contract.CustomerContactSnapshot)
            ? string.Empty
            : $"Contact: {contract.CustomerContactSnapshot}";

        foreach (var range in new[] { sheet.Range("A5:D5"), sheet.Range("A6:D8"), sheet.Range("A9:D9"), sheet.Range("E5:H5"), sheet.Range("E6:H8"), sheet.Range("E9:H9") })
        {
            range.Style.Alignment.WrapText = true;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        }

        sheet.Range("A11:B11").Merge().Value = "Contract No.";
        sheet.Range("C11:D11").Merge().Value = contract.ContractNumber;
        sheet.Range("E11:F11").Merge().Value = "Contract Date";
        sheet.Range("G11:H11").Merge().Value = contract.ContractDate;
        sheet.Range("G11:H11").Style.DateFormat.Format = "yyyy-mm-dd";

        sheet.Range("A12:B12").Merge().Value = "Customer PO";
        sheet.Range("C12:D12").Merge().Value = contract.CustomerPoNumber ?? string.Empty;
        sheet.Range("E12:F12").Merge().Value = "PO Date";
        if (contract.CustomerPoDate is not null)
        {
            sheet.Range("G12:H12").Merge().Value = contract.CustomerPoDate.Value;
            sheet.Range("G12:H12").Style.DateFormat.Format = "yyyy-mm-dd";
        }
        else
        {
            sheet.Range("G12:H12").Merge().Value = string.Empty;
        }

        sheet.Range("A13:B13").Merge().Value = "Payment Term";
        sheet.Range("C13:D13").Merge().Value = contract.PaymentTerm ?? string.Empty;
        sheet.Range("E13:F13").Merge().Value = "Delivery Term";
        sheet.Range("G13:H13").Merge().Value = contract.DeliveryTerm ?? string.Empty;

        sheet.Range("A14:B14").Merge().Value = "Lead Time";
        sheet.Range("C14:D14").Merge().Value = contract.LeadTime ?? string.Empty;
        sheet.Range("E14:F14").Merge().Value = "Currency";
        sheet.Range("G14:H14").Merge().Value = contract.Currency;

        var metadata = sheet.Range("A11:H14");
        metadata.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        metadata.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        metadata.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range("A11:B14").Style.Font.Bold = true;
        sheet.Range("E11:F14").Style.Font.Bold = true;
    }

    private static int BuildItems(IXLWorksheet sheet, Contract contract)
    {
        const int headerRow = 16;
        var headers = new[] { "No.", "Product", "Description", "Qty", "Unit", "Unit Price", "Disc. %", "Amount" };
        for (var col = 1; col <= headers.Length; col++)
            sheet.Cell(headerRow, col).Value = headers[col - 1];

        var header = sheet.Range(headerRow, 1, headerRow, 8);
        StyleSectionHeader(header);
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Row(headerRow).Height = 23;

        var orderedItems = contract.Items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToList();

        var row = headerRow + 1;
        foreach (var item in orderedItems)
        {
            sheet.Cell(row, 1).Value = row - headerRow;
            sheet.Cell(row, 2).Value = item.ArticleName;
            sheet.Cell(row, 3).Value = item.Description ?? string.Empty;
            sheet.Cell(row, 4).Value = item.Quantity;
            sheet.Cell(row, 5).Value = string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit;
            sheet.Cell(row, 6).Value = item.UnitPrice;
            sheet.Cell(row, 7).Value = item.DiscountPercent;
            sheet.Cell(row, 8).FormulaA1 = $"=ROUND(D{row}*F{row}*(1-G{row}/100),2)";

            sheet.Cell(row, 4).Style.NumberFormat.Format = "0.####";
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 7).Style.NumberFormat.Format = "0.##";
            sheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Range(row, 1, row, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.Range(row, 2, row, 3).Style.Alignment.WrapText = true;
            sheet.Range(row, 1, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            sheet.Row(row).Height = Math.Max(24, 15 + CountVisualLines(item.Description) * 12);
            row++;
        }

        var lastItemRow = row - 1;
        var itemArea = sheet.Range(headerRow, 1, lastItemRow, 8);
        itemArea.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        itemArea.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Range(row, 1, row, 6).Merge().Value = "TOTAL";
        sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 7).Value = contract.Currency;
        sheet.Cell(row, 7).Style.Font.Bold = true;
        sheet.Cell(row, 8).FormulaA1 = $"=SUM(H{headerRow + 1}:H{lastItemRow})";
        sheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 8).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 1, row, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Row(row).Height = 24;

        return row;
    }

    private static int BuildTermsAndSignatures(IXLWorksheet sheet, Contract contract, int startRow)
    {
        var row = startRow;

        row = AddTextSection(sheet, row, "Terms and Conditions", contract.TermsAndConditions);
        row = AddTextSection(sheet, row, "Remarks", contract.Remarks);
        row = AddTextSection(sheet, row, "Bank Information", contract.BankInformation);

        if (contract.SourceDocumentType != DocumentSourceType.None && !string.IsNullOrWhiteSpace(contract.SourceDocumentNumber))
        {
            sheet.Range(row, 1, row, 8).Merge().Value = $"Source document: {contract.SourceDocumentType} {contract.SourceDocumentNumber}";
            sheet.Range(row, 1, row, 8).Style.Font.Italic = true;
            sheet.Range(row, 1, row, 8).Style.Font.FontColor = XLColor.Gray;
            row += 2;
        }

        sheet.Range(row, 1, row, 4).Merge().Value = "SELLER SIGNATURE / COMPANY STAMP";
        sheet.Range(row, 5, row, 8).Merge().Value = "BUYER SIGNATURE / COMPANY STAMP";
        StyleSectionHeader(sheet.Range(row, 1, row, 4));
        StyleSectionHeader(sheet.Range(row, 5, row, 8));
        row++;

        var signatureTop = row;
        var signatureBottom = row + 7;
        var sellerBox = sheet.Range(signatureTop, 1, signatureBottom, 4).Merge();
        var buyerBox = sheet.Range(signatureTop, 5, signatureBottom, 8).Merge();
        sellerBox.Value = "Paste company stamp / signature here";
        buyerBox.Value = "Paste customer stamp / signature here";
        foreach (var box in new[] { sellerBox, buyerBox })
        {
            box.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            box.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            box.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            box.Style.Font.FontColor = XLColor.Gray;
            box.Style.Font.Italic = true;
        }
        for (var r = signatureTop; r <= signatureBottom; r++) sheet.Row(r).Height = 22;

        row = signatureBottom + 2;
        sheet.Range(row, 1, row, 8).Merge().Value = "This XLSX is intentionally editable for internal approval, customs/bank submission preparation, and insertion of signatures or company stamps.";
        sheet.Range(row, 1, row, 8).Style.Font.FontSize = 8;
        sheet.Range(row, 1, row, 8).Style.Font.FontColor = XLColor.Gray;
        sheet.Range(row, 1, row, 8).Style.Alignment.WrapText = true;

        return row;
    }

    private static int AddTextSection(IXLWorksheet sheet, int row, string title, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return row;

        sheet.Range(row, 1, row, 8).Merge().Value = title;
        StyleSectionHeader(sheet.Range(row, 1, row, 8));
        row++;

        var textRange = sheet.Range(row, 1, row + 2, 8).Merge();
        textRange.Value = value.Trim();
        textRange.Style.Alignment.WrapText = true;
        textRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        textRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        for (var r = row; r <= row + 2; r++) sheet.Row(r).Height = 22;
        return row + 4;
    }

    private static void StyleSectionHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightGray;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static string FormatContact(string? name, string? phone, string? email)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(name)) parts.Add($"Contact: {name}");
        if (!string.IsNullOrWhiteSpace(phone)) parts.Add($"Phone: {phone}");
        if (!string.IsNullOrWhiteSpace(email)) parts.Add($"Email: {email}");
        return string.Join("   ", parts);
    }

    private static int CountVisualLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 1;
        var explicitLines = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;
        var wrappedLines = (int)Math.Ceiling(value.Length / 48d);
        return Math.Clamp(Math.Max(explicitLines, wrappedLines), 1, 8);
    }
}
