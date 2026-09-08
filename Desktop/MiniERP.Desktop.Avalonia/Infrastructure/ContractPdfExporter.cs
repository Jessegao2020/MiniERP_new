using MiniERP.Domain;
using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

public static class ContractPdfExporter
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Left = 58f;
    private const float Right = 536f;
    private const float FooterLineY = 750f;

    private static readonly SKTypeface Regular = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Normal) ?? SKTypeface.Default;
    private static readonly SKTypeface Bold = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold) ?? SKTypeface.Default;
    private static readonly SKTypeface Italic = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Italic) ?? SKTypeface.Default;

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

            var y = pageIndex == 0 ? DrawHeader(canvas, contract) : DrawContinuationHeader(canvas, contract);
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
            y += 12f;

            if (page.IsLast)
                DrawClosingSections(canvas, contract, y);
            else
                DrawRight(canvas, "Continued on next page", Right, y + 8f, Paint(7f, Italic, new SKColor(90, 90, 90)));

            DrawFooter(canvas, pageIndex + 1, pages.Count);
            document.EndPage();
        }

        document.Close();
    }

    private static List<PagePlan> BuildPages(IReadOnlyList<ContractItem> items)
    {
        var pages = new List<PagePlan>();
        var current = new List<ContractItem>();
        var y = 350f;
        var startNo = 1;
        var currentStart = 1;

        foreach (var item in items)
        {
            var height = MeasureItemHeight(item);
            var limit = 610f;
            if (current.Count > 0 && y + height > limit)
            {
                pages.Add(new PagePlan(currentStart, current.ToList(), false));
                startNo += current.Count;
                currentStart = startNo;
                current.Clear();
                y = 142f;
            }
            current.Add(item);
            y += height;
        }

        pages.Add(new PagePlan(currentStart, current.ToList(), true));
        return pages;
    }

    private static float DrawHeader(SKCanvas canvas, Contract contract)
    {
        DrawBrandHeader(canvas, 70f, 108f);
        using var title = Paint(22f, Bold);
        DrawRight(canvas, "Sales Contract", Right, 146f, title);

        using var section = Paint(8.8f, Bold);
        using var text = Paint(8f, Regular);
        using var label = Paint(8f, Bold);

        canvas.DrawText("SELLER", Left, 180f, section);
        canvas.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", Left, 195f, text);
        canvas.DrawText("2699 Xiangyang North Street", Left, 207f, text);
        canvas.DrawText("071000 Baoding, China", Left, 219f, text);

        canvas.DrawText("BUYER", 310f, 180f, section);
        canvas.DrawText(contract.CustomerNameSnapshot ?? string.Empty, 310f, 195f, text);
        var buyerY = 207f;
        if (!string.IsNullOrWhiteSpace(contract.CustomerContactSnapshot))
        {
            canvas.DrawText(contract.CustomerContactSnapshot, 310f, buyerY, text);
            buyerY += 12f;
        }
        foreach (var line in SplitLines(contract.CustomerAddressSnapshot).Take(4))
        {
            canvas.DrawText(line, 310f, buyerY, text);
            buyerY += 12f;
        }

        var metaY = 258f;
        DrawMeta(canvas, "Contract No.", contract.ContractNumber, Left, 132f, metaY, label, text);
        DrawMeta(canvas, "Date", contract.ContractDate.ToString("MM.dd.yyyy"), 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Customer PO", contract.CustomerPoNumber ?? string.Empty, Left, 132f, metaY, label, text);
        DrawMeta(canvas, "PO Date", contract.CustomerPoDate?.ToString("MM.dd.yyyy") ?? string.Empty, 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Payment Term", contract.PaymentTerm ?? string.Empty, Left, 132f, metaY, label, text);
        DrawMeta(canvas, "Delivery Term", contract.DeliveryTerm ?? string.Empty, 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Lead Time", contract.LeadTime ?? string.Empty, Left, 132f, metaY, label, text);
        DrawMeta(canvas, "Contact", contract.SalesContactNameSnapshot ?? string.Empty, 310f, 380f, metaY, label, text);
        metaY += 13f;
        DrawMeta(canvas, "Phone", contract.SalesContactPhoneSnapshot ?? string.Empty, Left, 132f, metaY, label, text);
        DrawMeta(canvas, "Email", contract.SalesContactEmailSnapshot ?? string.Empty, 310f, 380f, metaY, label, text);

        return 320f;
    }

    private static float DrawContinuationHeader(SKCanvas canvas, Contract contract)
    {
        DrawBrandHeader(canvas, 55f, 88f);
        using var small = Paint(10f, Bold);
        canvas.DrawText($"Sales Contract {contract.ContractNumber} — continued", Left, 108f, small);
        return 116f;
    }

    private static float DrawTableHeader(SKCanvas canvas, string currency, float y)
    {
        using var header = Paint(8.5f, Bold);
        using var line = Stroke(0.8f, new SKColor(70, 70, 70));
        var baseline = y + 18f;
        canvas.DrawText("Item", Left + 2f, baseline, header);
        canvas.DrawText("Product", 92f, baseline, header);
        DrawRight(canvas, $"Unit Price ({currency})", 378f, baseline, header);
        DrawRight(canvas, "Qty", 425f, baseline, header);
        canvas.DrawText("Unit", 438f, baseline, header);
        DrawRight(canvas, "Amount", Right, baseline, header);
        canvas.DrawLine(Left, baseline + 5f, Right, baseline + 5f, line);
        return baseline + 17f;
    }

    private static float MeasureItemHeight(ContractItem item)
    {
        using var detail = Paint(7.1f, Regular);
        var lines = WrapText(item.Description, 220f, detail);
        return Math.Max(43f, 28f + lines.Count * 10f);
    }

    private static void DrawItem(SKCanvas canvas, int itemNo, ContractItem item, float y)
    {
        using var name = Paint(8.8f, Bold);
        using var detail = Paint(7.1f, Regular);
        using var normal = Paint(8.2f, Regular);
        var baseline = y + 12f;

        canvas.DrawText(itemNo.ToString(), Left + 10f, baseline, normal);
        canvas.DrawText(item.ArticleName, 92f, baseline, name);
        var netPrice = decimal.Round(item.UnitPrice * (1m - item.DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
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
        using var bold = Paint(8.5f, Bold);
        using var text = Paint(7.6f, Regular);
        using var line = Stroke(0.8f, SKColors.Black);

        DrawRight(canvas, "Total", 455f, y + 12f, bold);
        DrawRight(canvas, FormatMoney(contract.TotalAmount, contract.Currency), Right, y + 12f, bold);
        canvas.DrawLine(395f, y + 17f, Right, y + 17f, line);
        y += 36f;

        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(contract.PaymentTerm)) terms.Add($"Payment Term: {contract.PaymentTerm}");
        if (!string.IsNullOrWhiteSpace(contract.DeliveryTerm)) terms.Add($"Delivery Term: {contract.DeliveryTerm}");
        if (!string.IsNullOrWhiteSpace(contract.LeadTime)) terms.Add($"Lead Time: {contract.LeadTime}");
        if (!string.IsNullOrWhiteSpace(contract.BankInformation)) terms.Add($"Bank Information: {contract.BankInformation}");
        if (!string.IsNullOrWhiteSpace(contract.Remarks)) terms.Add($"Remarks: {contract.Remarks}");
        if (!string.IsNullOrWhiteSpace(contract.TermsAndConditions)) terms.Add(contract.TermsAndConditions);

        if (terms.Count > 0)
        {
            canvas.DrawText("Terms and Conditions", Left, y, bold);
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
            canvas.DrawText("For Seller", Left, signatureY, bold);
            canvas.DrawText("For Buyer", 335f, signatureY, bold);
            canvas.DrawLine(Left, signatureY + 35f, 240f, signatureY + 35f, line);
            canvas.DrawLine(335f, signatureY + 35f, Right, signatureY + 35f, line);
            canvas.DrawText("Authorized Signature / Stamp", Left, signatureY + 47f, text);
            canvas.DrawText("Authorized Signature / Stamp", 335f, signatureY + 47f, text);
        }
    }

    private static void DrawBrandHeader(SKCanvas canvas, float logoBaseline, float lineY)
    {
        var blue = new SKColor(0, 108, 181);
        using var fill = new SKPaint { IsAntialias = true, Color = blue, Style = SKPaintStyle.Fill };
        for (var i = 0; i < 4; i++)
        {
            var x = Left + i * 9f;
            using var path = new SKPath();
            path.MoveTo(x, logoBaseline - 7f);
            path.LineTo(x + 6f, logoBaseline - 15f);
            path.LineTo(x + 6f, logoBaseline + 1f);
            path.Close();
            canvas.DrawPath(path, fill);
        }
        using var logoText = Paint(17f, Bold, blue);
        canvas.DrawText("FORLINX", Left + 41f, logoBaseline - 2f, logoText);
        using var company = Paint(10.2f, Bold);
        using var tagline = Paint(7f, Italic);
        DrawRight(canvas, "Baoding Forlinx Embedded Technology Co., Ltd", Right, logoBaseline - 4f, company);
        DrawRight(canvas, "Trusted Designer & Manufacturer of System on Module", Right, logoBaseline + 9f, tagline);
        using var rule = Stroke(0.8f, new SKColor(70, 70, 70));
        canvas.DrawLine(Left, lineY, Right, lineY, rule);
    }

    private static void DrawFooter(SKCanvas canvas, int pageNumber, int pageCount)
    {
        using var rule = Stroke(0.75f, new SKColor(70, 70, 70));
        using var company = Paint(6.4f, Bold);
        using var label = Paint(6.4f, Bold);
        using var text = Paint(6.4f, Regular);
        using var page = Paint(8f, Regular, new SKColor(90, 90, 90));
        canvas.DrawLine(Left, FooterLineY, Right, FooterLineY, rule);

        const float y = 770f;
        const float step = 10.5f;
        canvas.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", Left + 2f, y, company);
        canvas.DrawText("2699 Xiangyang North Street", Left + 2f, y + step, text);
        canvas.DrawText("071000 Baoding", Left + 2f, y + step * 2f, text);
        canvas.DrawText("China", Left + 2f, y + step * 3f, text);

        DrawMeta(canvas, "Bank Name:", "China Construction Bank", 286f, 359f, y, label, text);
        DrawMeta(canvas, "Bank Address:", "345 Longxing West Rd, Baoding, China", 286f, 359f, y + step, label, text);
        DrawMeta(canvas, "Bank Account:", "1301 4600 6002 2010 0241", 286f, 359f, y + step * 2f, label, text);
        DrawMeta(canvas, "Swift Code:", "PCBCCNBJ", 286f, 359f, y + step * 3f, label, text);
        DrawCenter(canvas, $"Page {pageNumber} of {pageCount}", PageWidth / 2f, 826f, page);
    }

    private static void DrawMeta(SKCanvas canvas, string label, string value, float labelX, float valueX, float y, SKPaint labelPaint, SKPaint valuePaint)
    {
        canvas.DrawText(label, labelX, y, labelPaint);
        canvas.DrawText(value ?? string.Empty, valueX, y, valuePaint);
    }

    private static List<string> WrapText(string? value, float width, SKPaint paint)
    {
        var result = new List<string>();
        foreach (var paragraph in SplitLines(value))
        {
            if (string.IsNullOrWhiteSpace(paragraph)) { result.Add(string.Empty); continue; }
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (paint.MeasureText(candidate) <= width) { current = candidate; continue; }
                if (!string.IsNullOrEmpty(current)) result.Add(current);
                current = word;
            }
            if (!string.IsNullOrEmpty(current)) result.Add(current);
        }
        return result.Count == 0 ? new List<string>() : result;
    }

    private static IEnumerable<string> SplitLines(string? value)
        => (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static string FormatMoney(decimal value, string currency)
        => string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? $"${value:N2}" : $"CNY {value:N2}";

    private static string FormatQuantity(decimal value)
        => value == decimal.Truncate(value) ? value.ToString("0") : value.ToString("0.####");

    private static SKPaint Paint(float size, SKTypeface typeface, SKColor? color = null)
        => new() { IsAntialias = true, Typeface = typeface, TextSize = size, Color = color ?? SKColors.Black };

    private static SKPaint Stroke(float width, SKColor color)
        => new() { IsAntialias = true, Color = color, StrokeWidth = width, Style = SKPaintStyle.Stroke };

    private static void DrawRight(SKCanvas canvas, string text, float x, float y, SKPaint paint)
        => canvas.DrawText(text ?? string.Empty, x - paint.MeasureText(text ?? string.Empty), y, paint);

    private static void DrawCenter(SKCanvas canvas, string text, float x, float y, SKPaint paint)
        => canvas.DrawText(text ?? string.Empty, x - paint.MeasureText(text ?? string.Empty) / 2f, y, paint);

    private sealed record PagePlan(int StartItemNumber, List<ContractItem> Items, bool IsLast);
}
