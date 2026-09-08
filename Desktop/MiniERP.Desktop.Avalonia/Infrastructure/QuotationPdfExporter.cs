using MiniERP.Domain;
using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

public static class QuotationPdfExporter
{
    // A4 portrait in PDF points.
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Left = 58f;
    private const float Right = 536f;
    private const float FooterLineY = 718f;

    private const float ItemCenterX = 72f;
    private const float ProductX = 88f;
    private const float ProductWidth = 215f;
    private const float UnitPriceRightX = 350f;
    private const float QuantityRightX = 411f;
    private const float UnitCenterX = 440f;
    private const float AmountRightX = 536f;

    private static readonly SKTypeface Regular = FindTypeface(SKFontStyle.Normal);
    private static readonly SKTypeface Bold = FindTypeface(SKFontStyle.Bold);
    private static readonly SKTypeface Italic = FindTypeface(SKFontStyle.Italic);
    private static readonly SKTypeface BoldItalic = FindTypeface(new SKFontStyle(SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic));

    public static void Export(Quotation quotation, Stream output)
    {
        if (quotation.Items.Count == 0)
            throw new InvalidOperationException("A quotation needs at least one item before it can be exported.");

        var plans = BuildPagePlans(quotation.Items.OrderBy(item => item.Id).ToList());
        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        for (var pageIndex = 0; pageIndex < plans.Count; pageIndex++)
        {
            var plan = plans[pageIndex];
            var canvas = document.BeginPage(PageWidth, PageHeight);
            canvas.Clear(SKColors.White);

            var y = plan.IsFirstPage
                ? DrawFirstPageHeader(canvas, quotation)
                : DrawContinuationHeader(canvas, quotation);

            if (plan.Items.Count > 0)
            {
                y = DrawTableHeader(canvas, quotation.Currency, y);
                var itemNumber = plan.StartItemNumber;

                foreach (var item in plan.Items)
                {
                    var rowHeight = MeasureItemHeight(item);
                    DrawItem(canvas, itemNumber++, item, y, rowHeight);
                    y += rowHeight;
                }

                using var tableEnd = Stroke(0.8f, SKColors.Black);
                canvas.DrawLine(Left, y + 2f, Right, y + 2f, tableEnd);
                y += 7f;
            }

            if (plan.DrawTotal)
                DrawTotal(canvas, quotation, y);

            DrawFooter(canvas, pageIndex + 1, plans.Count);
            document.EndPage();
        }

        document.Close();
    }

    private static List<PagePlan> BuildPagePlans(IReadOnlyList<QuotationItem> items)
    {
        var pages = new List<PagePlan>();
        var current = new List<QuotationItem>();
        var first = true;
        var startNumber = 1;
        var y = FirstTableDataY;
        var currentStartNumber = startNumber;

        foreach (var item in items)
        {
            var height = MeasureItemHeight(item);
            var limit = 684f;

            if (current.Count > 0 && y + height > limit)
            {
                pages.Add(new PagePlan(first, currentStartNumber, current.ToList(), false));
                startNumber += current.Count;
                current.Clear();
                first = false;
                currentStartNumber = startNumber;
                y = ContinuationTableDataY;
            }

            current.Add(item);
            y += height;
        }

        // Reserve enough room for the Total block on the final page.
        if (current.Count > 0 && y + 34f > 704f)
        {
            pages.Add(new PagePlan(first, currentStartNumber, current.ToList(), false));
            startNumber += current.Count;
            pages.Add(new PagePlan(false, startNumber, new List<QuotationItem>(), true));
        }
        else
        {
            pages.Add(new PagePlan(first, currentStartNumber, current.ToList(), true));
        }

        return pages;
    }

    private const float FirstTableDataY = 354f;
    private const float ContinuationTableDataY = 140f;

    private static float DrawFirstPageHeader(SKCanvas canvas, Quotation quotation)
    {
        DrawBrandHeader(canvas, 71f, 108f);

        using var title = Paint(24f, Bold);
        RightText(canvas, "Quotation", Right - 3f, 146f, title);

        using var customerName = Paint(9.3f, Bold);
        using var customerText = Paint(8.4f, Regular);

        var customerY = 178f;
        canvas.DrawText(quotation.CustomerNameSnapshot ?? string.Empty, Left + 3f, customerY, customerName);
        customerY += 13f;

        if (!string.IsNullOrWhiteSpace(quotation.CustomerContactSnapshot))
        {
            canvas.DrawText(quotation.CustomerContactSnapshot, Left + 3f, customerY, customerText);
            customerY += 13f;
        }

        if (!string.IsNullOrWhiteSpace(quotation.CustomerAddressSnapshot))
        {
            foreach (var line in SplitLines(quotation.CustomerAddressSnapshot).Take(4))
            {
                canvas.DrawText(line, Left + 3f, customerY, customerText);
                customerY += 13f;
            }
        }

        using var label = Paint(8.5f, Bold);
        using var value = Paint(8.3f, Regular);
        var metaY = 178f;
        const float metaStep = 13f;
        const float labelX = 354f;
        const float valueX = 443f;

        DrawMeta(canvas, "Date", quotation.QuotationDate.ToString("MM.dd.yyyy"), labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Quotation No.", quotation.QuotationNumber, labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Contact", quotation.SalesContactNameSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Phone", quotation.SalesContactPhoneSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Email", quotation.SalesContactEmailSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Payment Term", quotation.PaymentTerm ?? string.Empty, labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Delivery Term", quotation.DeliveryTerm ?? string.Empty, labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Lead Time", quotation.LeadTime ?? string.Empty, labelX, valueX, metaY, label, value); metaY += metaStep;
        DrawMeta(canvas, "Validity", FormatValidity(quotation), labelX, valueX, metaY, label, value);

        return 317f;
    }

    private static float DrawContinuationHeader(SKCanvas canvas, Quotation quotation)
    {
        DrawBrandHeader(canvas, 55f, 88f);
        using var smallTitle = Paint(10f, Bold);
        canvas.DrawText($"Quotation {quotation.QuotationNumber} — continued", Left, 108f, smallTitle);
        return 116f;
    }

    private static void DrawBrandHeader(SKCanvas canvas, float logoBaseline, float lineY)
    {
        var blue = new SKColor(0, 108, 181);
        DrawVectorLogo(canvas, Left, logoBaseline - 16f, blue);

        using var company = Paint(10.2f, Bold);
        using var tagline = Paint(7.1f, Italic);
        RightText(canvas, "Baoding Forlinx Embedded Technology Co., Ltd", Right - 3f, logoBaseline - 4f, company);
        RightText(canvas, "Trusted Designer & Manufacturer of System on Module", Right - 3f, logoBaseline + 9f, tagline);

        using var rule = Stroke(0.8f, new SKColor(70, 70, 70));
        canvas.DrawLine(Left, lineY, Right, lineY, rule);
    }

    private static void DrawVectorLogo(SKCanvas canvas, float x, float y, SKColor blue)
    {
        using var fill = new SKPaint { IsAntialias = true, Color = blue, Style = SKPaintStyle.Fill };
        for (var i = 0; i < 4; i++)
        {
            var offset = i * 9f;
            using var path = new SKPath();
            path.MoveTo(x + offset, y + 9f);
            path.LineTo(x + offset + 6f, y + 1f);
            path.LineTo(x + offset + 6f, y + 17f);
            path.Close();
            canvas.DrawPath(path, fill);
        }

        using var word = Paint(17f, BoldItalic, blue);
        canvas.DrawText("FORLINX", x + 41f, y + 14f, word);
        using var embedded = Paint(5.8f, Bold);
        canvas.DrawText("Embedded", x + 103f, y + 22f, embedded);
    }

    private static float DrawTableHeader(SKCanvas canvas, string currency, float y)
    {
        using var header = Paint(9f, Bold);
        using var currencyPaint = Paint(8f, Bold);
        using var line = Stroke(0.8f, new SKColor(70, 70, 70));

        CenterText(canvas, $"({currency})", 337f, y + 3f, currencyPaint);
        var baseline = y + 17f;
        CenterText(canvas, "Item", ItemCenterX, baseline, header);
        canvas.DrawText("Product", ProductX, baseline, header);
        RightText(canvas, "Unit Price", UnitPriceRightX, baseline, header);
        RightText(canvas, "Quantity", QuantityRightX, baseline, header);
        CenterText(canvas, "Unit", UnitCenterX, baseline, header);
        RightText(canvas, "Amount", AmountRightX, baseline, header);
        canvas.DrawLine(Left, baseline + 5f, Right, baseline + 5f, line);
        return baseline + 17f;
    }

    private static float MeasureItemHeight(QuotationItem item)
    {
        using var detail = Paint(7.2f, Regular);
        var lines = WrapText(item.Description, ProductWidth, detail);
        return Math.Max(45f, 30f + lines.Count * 10f);
    }

    private static void DrawItem(SKCanvas canvas, int itemNumber, QuotationItem item, float y, float height)
    {
        using var name = Paint(9f, Bold);
        using var detail = Paint(7.2f, Regular);
        using var normal = Paint(8.5f, Regular);

        var baseline = y + 12f;
        CenterText(canvas, itemNumber.ToString(), ItemCenterX, baseline, normal);
        canvas.DrawText(item.ArticleName, ProductX, baseline, name);

        var netUnitPrice = decimal.Round(
            item.UnitPrice * (1m - item.DiscountPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);

        RightText(canvas, FormatMoney(netUnitPrice, item.Currency), UnitPriceRightX, baseline, normal);
        RightText(canvas, FormatQuantity(item.Quantity), QuantityRightX, baseline, normal);
        CenterText(canvas, string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit, UnitCenterX, baseline, normal);
        RightText(canvas, FormatMoney(item.LineTotal, item.Currency), AmountRightX, baseline, normal);

        var detailY = baseline + 14f;
        foreach (var line in WrapText(item.Description, ProductWidth, detail))
        {
            canvas.DrawText(line, ProductX, detailY, detail);
            detailY += 10f;
        }
    }

    private static void DrawTotal(SKCanvas canvas, Quotation quotation, float y)
    {
        using var line = Stroke(0.8f, SKColors.Black);
        using var label = Paint(9f, Bold);
        using var amount = Paint(9f, Bold);

        var totalLeft = 350f;

        // The product table already has its closing rule. Do not draw another
        // top rule here, otherwise the Total block appears to have a double line.
        var baseline = y + 15f;
        RightText(canvas, "Total", 438f, baseline, label);
        RightText(canvas, FormatMoney(quotation.TotalAmount, quotation.Currency), Right, baseline, amount);
        canvas.DrawLine(totalLeft, baseline + 5f, Right, baseline + 5f, line);
    }

    private static void DrawFooter(SKCanvas canvas, int pageNumber, int pageCount)
    {
        using var rule = Stroke(0.75f, new SKColor(70, 70, 70));
        using var company = Paint(6.5f, Bold);
        using var label = Paint(6.5f, Bold);
        using var text = Paint(6.5f, Regular);
        using var page = Paint(8f, Regular, new SKColor(90, 90, 90));

        canvas.DrawLine(Left, FooterLineY, Right, FooterLineY, rule);

        // Footer text should read as a compact information block rather than body text.
        const float y = 742f;
        const float lineStep = 11.5f;
        canvas.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", Left + 2f, y, company);
        canvas.DrawText("2699 Xiangyang North Street", Left + 2f, y + lineStep, text);
        canvas.DrawText("071000 Baoding", Left + 2f, y + lineStep * 2f, text);
        canvas.DrawText("China", Left + 2f, y + lineStep * 3f, text);

        const float bankLabelX = 286f;
        const float bankValueX = 359f;
        DrawMeta(canvas, "Bank Name:", "China Construction Bank", bankLabelX, bankValueX, y, label, text);
        DrawMeta(canvas, "Bank Address:", "345 Longxing West Rd, Baoding, China", bankLabelX, bankValueX, y + lineStep, label, text);
        DrawMeta(canvas, "Bank Account:", "1301 4600 6002 2010 0241", bankLabelX, bankValueX, y + lineStep * 2f, label, text);
        DrawMeta(canvas, "Swift Code:", "PCBCCNBJ", bankLabelX, bankValueX, y + lineStep * 3f, label, text);

        CenterText(canvas, $"Page {pageNumber} of {pageCount}", PageWidth / 2f, 826f, page);
    }

    private static void DrawMeta(SKCanvas canvas, string label, string value, float labelX, float valueX, float y, SKPaint labelPaint, SKPaint valuePaint)
    {
        canvas.DrawText(label, labelX, y, labelPaint);
        canvas.DrawText(value ?? string.Empty, valueX, y, valuePaint);
    }

    private static string FormatValidity(Quotation quotation)
    {
        if (quotation.ValidUntil is null)
            return string.Empty;

        var days = (quotation.ValidUntil.Value.Date - quotation.QuotationDate.Date).Days;
        return days >= 0 ? $"{days} days" : quotation.ValidUntil.Value.ToString("MM.dd.yyyy");
    }

    private static string FormatMoney(decimal value, string currency)
        => string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase)
            ? $"${value:N2}"
            : $"CNY {value:N2}";

    private static string FormatQuantity(decimal value)
        => value == decimal.Truncate(value) ? value.ToString("0") : value.ToString("0.####");

    private static IEnumerable<string> SplitLines(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(line => !string.IsNullOrWhiteSpace(line));

    private static List<string> WrapText(string? text, float maxWidth, SKPaint paint)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (var paragraph in SplitLines(text))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            var current = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = current + " " + words[i];
                if (paint.MeasureText(candidate) <= maxWidth)
                {
                    current = candidate;
                }
                else
                {
                    result.Add(current);
                    current = words[i];
                }
            }
            result.Add(current);
        }

        return result;
    }

    private static SKPaint Paint(float size, SKTypeface typeface, SKColor? color = null)
        => new()
        {
            IsAntialias = true,
            TextSize = size,
            Typeface = typeface,
            Color = color ?? SKColors.Black
        };

    private static SKPaint Stroke(float width, SKColor color)
        => new()
        {
            IsAntialias = true,
            StrokeWidth = width,
            Color = color,
            Style = SKPaintStyle.Stroke
        };

    private static void CenterText(SKCanvas canvas, string text, float centerX, float baseline, SKPaint paint)
        => canvas.DrawText(text, centerX - paint.MeasureText(text) / 2f, baseline, paint);

    private static void RightText(SKCanvas canvas, string text, float rightX, float baseline, SKPaint paint)
        => canvas.DrawText(text, rightX - paint.MeasureText(text), baseline, paint);

    private static SKTypeface FindTypeface(SKFontStyle style)
    {
        foreach (var family in new[] { "Arial", "Liberation Sans", "DejaVu Sans" })
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null) return typeface;
        }
        return SKTypeface.Default;
    }

    private sealed record PagePlan(bool IsFirstPage, int StartItemNumber, List<QuotationItem> Items, bool DrawTotal);
}
