using MiniERP.Domain;
using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

public static class ContractPdfExporter
{
    private const float PageWidth = DocumentPdfStyle.PageWidth;
    private const float PageHeight = DocumentPdfStyle.PageHeight;
    private const float Left = DocumentPdfStyle.Left;
    private const float Right = DocumentPdfStyle.Right;

    // Non-final pages only need room for the continuation marker. The final item
    // is checked against the conservative final-page limit so Total / Terms /
    // signatures remain above the approved Quotation footer.
    private const float FirstPageItemsStartY = 350f;
    private const float ContinuationItemsStartY = 142f;
    private const float NonFinalItemsLimitY = 700f;
    private const float FinalItemsLimitY = 610f;

    private static readonly SKTypeface Regular = DocumentPdfStyle.Regular;
    private static readonly SKTypeface Bold = DocumentPdfStyle.Bold;
    private static readonly SKTypeface Italic = DocumentPdfStyle.Italic;

    public static void Export(Contract contract, Stream output)
    {
        if (contract.Items.Count == 0)
            throw new InvalidOperationException("A contract needs at least one item before it can be exported.");

        var items = contract.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        var pages = BuildPages(items);

        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = pages[pageIndex];
            var canvas = document.BeginPage(PageWidth, PageHeight);
            canvas.Clear(SKColors.White);

            var y = pageIndex == 0
                ? DrawHeader(canvas, contract)
                : DrawContinuationHeader(canvas, contract);
            y = DrawTableHeader(canvas, contract.Currency, y);

            var itemNo = page.StartItemNumber;
            foreach (var item in page.Items)
            {
                var height = MeasureItemHeight(item);
                DrawItem(canvas, itemNo++, item, y);
                y += height;
            }

            using (var rule = Stroke(0.8f, SKColors.Black))
                canvas.DrawLine(Left, y + 2f, Right, y + 2f, rule);
            y += 7f;

            if (page.IsLast)
                DrawClosingSections(canvas, contract, y);
            else
                DocumentPdfStyle.DrawContinuedOnNextPage(canvas, y + 8f);

            DocumentPdfStyle.DrawFooter(canvas, pageIndex + 1, pages.Count);
            document.EndPage();
        }

        document.Close();
    }

    private static List<PagePlan> BuildPages(IReadOnlyList<ContractItem> items)
    {
        var pages = new List<PagePlan>();
        var current = new List<ContractItem>();
        var y = FirstPageItemsStartY;
        var startNo = 1;
        var currentStart = 1;

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var height = MeasureItemHeight(item);
            var isLastItem = index == items.Count - 1;
            var limit = isLastItem ? FinalItemsLimitY : NonFinalItemsLimitY;

            if (current.Count > 0 && y + height > limit)
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

    private static float DrawHeader(SKCanvas canvas, Contract contract)
    {
        DocumentPdfStyle.DrawBrandHeader(canvas, 71f, 108f);

        using var title = Paint(24f, Bold);
        DrawRight(canvas, "Sales Contract", Right - 3f, 146f, title);

        using var section = Paint(8.8f, Bold);
        using var text = Paint(8.4f, Regular);
        using var label = Paint(8.5f, Bold);

        canvas.DrawText("SELLER", Left + 3f, 180f, section);
        canvas.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", Left + 3f, 195f, text);
        canvas.DrawText("2699 Xiangyang North Street", Left + 3f, 207f, text);
        canvas.DrawText("071000 Baoding, China", Left + 3f, 219f, text);

        canvas.DrawText("BUYER", 310f, 180f, section);
        canvas.DrawText(contract.CustomerNameSnapshot ?? string.Empty, 310f, 195f, text);
        var buyerY = 207f;
        if (!string.IsNullOrWhiteSpace(contract.CustomerContactSnapshot))
        {
            canvas.DrawText(contract.CustomerContactSnapshot, 310f, buyerY, text);
            buyerY += 12f;
        }

        var printableAddress = CountryRegionNames.ExpandAddressCountry(contract.CustomerAddressSnapshot);
        foreach (var line in SplitLines(printableAddress).Take(4))
        {
            canvas.DrawText(line, 310f, buyerY, text);
            buyerY += 12f;
        }

        var metaY = 258f;
        DrawMeta(canvas, "Contract No.", contract.ContractNumber, Left + 3f, 132f, metaY, label, text);
        DrawMeta(canvas, "Date", contract.ContractDate.ToString("MM.dd.yyyy"), 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Customer PO", contract.CustomerPoNumber ?? string.Empty, Left + 3f, 132f, metaY, label, text);
        DrawMeta(canvas, "PO Date", contract.CustomerPoDate?.ToString("MM.dd.yyyy") ?? string.Empty, 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Payment Term", contract.PaymentTerm ?? string.Empty, Left + 3f, 132f, metaY, label, text);
        DrawMeta(canvas, "Delivery Term", contract.DeliveryTerm ?? string.Empty, 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Lead Time", contract.LeadTime ?? string.Empty, Left + 3f, 132f, metaY, label, text);
        DrawMeta(canvas, "Contact", contract.SalesContactNameSnapshot ?? string.Empty, 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Phone", contract.SalesContactPhoneSnapshot ?? string.Empty, Left + 3f, 132f, metaY, label, text);
        DrawMeta(canvas, "Email", contract.SalesContactEmailSnapshot ?? string.Empty, 310f, 380f, metaY, label, text);

        return 320f;
    }

    private static float DrawContinuationHeader(SKCanvas canvas, Contract contract)
    {
        DocumentPdfStyle.DrawBrandHeader(canvas, 55f, 88f);
        using var small = Paint(10f, Bold);
        canvas.DrawText($"Sales Contract {contract.ContractNumber} — continued", Left, 108f, small);
        return 116f;
    }

    private static float DrawTableHeader(SKCanvas canvas, string currency, float y)
    {
        using var header = Paint(9f, Bold);
        using var currencyPaint = Paint(8f, Bold);
        using var line = Stroke(0.8f, new SKColor(70, 70, 70));

        DocumentPdfStyle.CenterText(canvas, $"({currency})", 355f, y + 3f, currencyPaint);
        var baseline = y + 17f;
        canvas.DrawText("Item", Left + 2f, baseline, header);
        canvas.DrawText("Product", 92f, baseline, header);
        DrawRight(canvas, "Unit Price", 378f, baseline, header);
        DrawRight(canvas, "Qty", 425f, baseline, header);
        canvas.DrawText("Unit", 438f, baseline, header);
        DrawRight(canvas, "Amount", Right, baseline, header);
        canvas.DrawLine(Left, baseline + 5f, Right, baseline + 5f, line);
        return baseline + 17f;
    }

    private static float MeasureItemHeight(ContractItem item)
    {
        using var detail = Paint(7.2f, Regular);
        var lines = WrapText(item.Description, 220f, detail);
        return Math.Max(45f, 30f + lines.Count * 10f);
    }

    private static void DrawItem(SKCanvas canvas, int itemNo, ContractItem item, float y)
    {
        using var name = Paint(9f, Bold);
        using var detail = Paint(7.2f, Regular);
        using var normal = Paint(8.5f, Regular);
        var baseline = y + 12f;

        canvas.DrawText(itemNo.ToString(), Left + 10f, baseline, normal);
        canvas.DrawText(item.ArticleName, 92f, baseline, name);
        var netPrice = decimal.Round(
            item.UnitPrice * (1m - item.DiscountPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);
        DrawRight(canvas, FormatMoney(netPrice, item.Currency), 378f, baseline, normal);
        DrawRight(canvas, FormatQuantity(item.Quantity), 425f, baseline, normal);
        canvas.DrawText(string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit, 438f, baseline, normal);
        DrawRight(canvas, FormatMoney(item.LineTotal, item.Currency), Right, baseline, normal);

        var detailY = baseline + 14f;
        foreach (var line in WrapText(item.Description, 220f, detail))
        {
            canvas.DrawText(line, 92f, detailY, detail);
            detailY += 10f;
        }
    }

    private static void DrawClosingSections(SKCanvas canvas, Contract contract, float y)
    {
        using var bold = Paint(9f, Bold);
        using var section = Paint(8.5f, Bold);
        using var text = Paint(7.6f, Regular);
        using var line = Stroke(0.8f, SKColors.Black);

        DrawRight(canvas, "Total", 438f, y + 15f, bold);
        DrawRight(canvas, FormatMoney(contract.TotalAmount, contract.Currency), Right, y + 15f, bold);
        canvas.DrawLine(350f, y + 20f, Right, y + 20f, line);
        y += 39f;

        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(contract.PaymentTerm)) terms.Add($"Payment Term: {contract.PaymentTerm}");
        if (!string.IsNullOrWhiteSpace(contract.DeliveryTerm)) terms.Add($"Delivery Term: {contract.DeliveryTerm}");
        if (!string.IsNullOrWhiteSpace(contract.LeadTime)) terms.Add($"Lead Time: {contract.LeadTime}");
        if (!string.IsNullOrWhiteSpace(contract.BankInformation)) terms.Add($"Bank Information: {contract.BankInformation}");
        if (!string.IsNullOrWhiteSpace(contract.Remarks)) terms.Add($"Remarks: {contract.Remarks}");
        if (!string.IsNullOrWhiteSpace(contract.TermsAndConditions)) terms.Add(contract.TermsAndConditions);

        if (terms.Count > 0)
        {
            canvas.DrawText("Terms and Conditions", Left, y, section);
            y += 13f;
            foreach (var paragraph in terms)
            {
                foreach (var wrapped in WrapText(paragraph, Right - Left, text))
                {
                    if (y > 690f) break;
                    canvas.DrawText(wrapped, Left, y, text);
                    y += 10f;
                }
                y += 3f;
            }
        }

        var signatureY = Math.Max(y + 18f, 675f);
        if (signatureY < 720f)
        {
            canvas.DrawText("For Seller", Left, signatureY, section);
            canvas.DrawText("For Buyer", 335f, signatureY, section);
            canvas.DrawLine(Left, signatureY + 35f, 240f, signatureY + 35f, line);
            canvas.DrawLine(335f, signatureY + 35f, Right, signatureY + 35f, line);
            canvas.DrawText("Authorized Signature / Stamp", Left, signatureY + 47f, text);
            canvas.DrawText("Authorized Signature / Stamp", 335f, signatureY + 47f, text);
        }
    }

    private static void DrawMeta(
        SKCanvas canvas,
        string label,
        string value,
        float labelX,
        float valueX,
        float y,
        SKPaint labelPaint,
        SKPaint valuePaint)
    {
        canvas.DrawText(label, labelX, y, labelPaint);
        canvas.DrawText(value ?? string.Empty, valueX, y, valuePaint);
    }

    private static List<string> WrapText(string? value, float width, SKPaint paint)
    {
        var result = new List<string>();
        foreach (var paragraph in SplitLines(value))
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

                if (!string.IsNullOrEmpty(current)) result.Add(current);
                current = word;
            }

            if (!string.IsNullOrEmpty(current)) result.Add(current);
        }

        return result;
    }

    private static IEnumerable<string> SplitLines(string? value)
        => (value ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line));

    private static string FormatMoney(decimal value, string currency)
        => string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase)
            ? $"${value:N2}"
            : $"CNY {value:N2}";

    private static string FormatQuantity(decimal value)
        => value == decimal.Truncate(value) ? value.ToString("0") : value.ToString("0.####");

    private static SKPaint Paint(float size, SKTypeface typeface, SKColor? color = null)
        => DocumentPdfStyle.Paint(size, typeface, color);

    private static SKPaint Stroke(float width, SKColor color)
        => DocumentPdfStyle.Stroke(width, color);

    private static void DrawRight(SKCanvas canvas, string text, float x, float y, SKPaint paint)
        => DocumentPdfStyle.RightText(canvas, text, x, y, paint);

    private sealed record PagePlan(int StartItemNumber, List<ContractItem> Items, bool IsLast);
}
