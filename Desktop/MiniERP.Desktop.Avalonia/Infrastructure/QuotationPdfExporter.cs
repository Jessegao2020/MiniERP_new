using MiniERP.Domain;
using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

public static class QuotationPdfExporter
{
    private const float W = 842f;
    private const float H = 595f;
    private const float L = 24f;
    private const float FooterTop = 535f;
    private const float ItemW = 28f;
    private const float ProductW = 196f;
    private const float UnitW = 38f;
    private const float TierW = 177f;
    private const float QtyW = 39f;
    private const float PriceW = 62f;
    private const float AmountW = 76f;

    private static readonly SKTypeface Regular = Typeface(SKFontStyle.Normal);
    private static readonly SKTypeface Bold = Typeface(SKFontStyle.Bold);
    private static readonly SKTypeface Italic = Typeface(SKFontStyle.Italic);

    public static void Export(Quotation q, Stream output)
    {
        if (q.Items.Count == 0)
            throw new InvalidOperationException("A quotation needs at least one item before it can be exported.");

        using var doc = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        var canvas = doc.BeginPage(W, H);
        var y = FirstHeader(canvas, q);
        y = TableHeader(canvas, q, y);

        var number = 1;
        foreach (var item in q.Items)
        {
            var height = ItemHeight(item);
            if (y + height > FooterTop)
            {
                Footer(canvas, false);
                doc.EndPage();
                canvas = doc.BeginPage(W, H);
                y = ContinuationHeader(canvas);
                y = TableHeader(canvas, q, y);
            }

            ItemRow(canvas, number++, item, y, height);
            y += height;
        }

        if (y + 74f > FooterTop)
        {
            Footer(canvas, false);
            doc.EndPage();
            canvas = doc.BeginPage(W, H);
            y = ContinuationHeader(canvas) + 10f;
        }

        Totals(canvas, q, y);
        Footer(canvas, true);
        doc.EndPage();
        doc.Close();
    }

    private static float FirstHeader(SKCanvas c, Quotation q)
    {
        CompanyHeader(c);

        using var title = P(19f, Bold);
        Center(c, "Quotation", W / 2f, 83f, title);

        using var customer = P(8.5f, Bold);
        using var small = P(7.3f, Regular);
        using var label = P(7.5f, Bold);
        using var value = P(7.5f, Regular);

        c.DrawText(q.CustomerNameSnapshot ?? q.Customer?.Name ?? string.Empty, L, 112f, customer);

        var contact = q.CustomerContactSnapshot;
        if (!string.IsNullOrWhiteSpace(contact))
            c.DrawText(contact, L, 126f, small);

        var lx = 320f;
        var vx = 405f;
        var y = 108f;
        const float step = 12.2f;

        Meta(c, "Date", q.QuotationDate.ToString("dd.MM.yyyy"), lx, vx, y, label, value); y += step;
        Meta(c, "Quotation No.", q.QuotationNumber, lx, vx, y, label, value); y += step;
        Meta(c, "Contact", q.SalesContactNameSnapshot ?? q.User?.Name ?? string.Empty, lx, vx, y, label, value); y += step;
        Meta(c, "Phone", q.SalesContactPhoneSnapshot ?? q.User?.Phone ?? string.Empty, lx, vx, y, label, value); y += step;
        Meta(c, "Email", q.SalesContactEmailSnapshot ?? q.User?.Email ?? string.Empty, lx, vx, y, label, value); y += step;
        Meta(c, "Payment Term", q.PaymentTerm ?? string.Empty, lx, vx, y, label, value); y += step;
        Meta(c, "Delivery Term", q.DeliveryTerm ?? string.Empty, lx, vx, y, label, value); y += step;
        Meta(c, "Lead Time", q.LeadTime ?? string.Empty, lx, vx, y, label, value); y += step;
        Meta(c, "Validity", Validity(q), lx, vx, y, label, value);

        using var term = P(6.8f, Regular);
        c.DrawText(string.IsNullOrWhiteSpace(q.PaymentTerm) ? "Payment term:" : $"Payment term: {q.PaymentTerm.Trim()}", L, 213f, term);
        c.DrawText("Warranty: see attached warranty document", L, 227f, term);

        return 244f;
    }

    private static float ContinuationHeader(SKCanvas c)
    {
        CompanyHeader(c);
        return 66f;
    }

    private static void CompanyHeader(SKCanvas c)
    {
        using var company = P(10.5f, Bold);
        using var tagline = P(6.5f, Italic);
        using var line = Stroke(0.8f, SKColors.Black);

        Center(c, "Baoding Forlinx Embedded Technology Co., Ltd", W / 2f, 20f, company);
        Center(c, "Trusted Designer & Manufacturer of System on Module", W / 2f, 32f, tagline);
        c.DrawLine(L, 46f, W - L, 46f, line);
    }

    private static float TableHeader(SKCanvas c, Quotation q, float y)
    {
        using var header = P(7.2f, Bold);
        using var sub = P(6.7f, Bold);
        using var line = Stroke(0.8f, SKColors.Black);
        using var light = Stroke(0.35f, new SKColor(205, 205, 205));

        var x = Xs();
        var middle = y + 17f;
        var bottom = middle + 25f;

        c.DrawLine(x.Left, y, x.Right, y, line);
        c.DrawLine(x.Left, middle, x.Right, middle, light);
        c.DrawLine(x.Left, bottom, x.Right, bottom, line);

        Center(c, "Item", x.Item + ItemW / 2f, bottom - 8f, header);
        Center(c, "Product", x.Product + ProductW / 2f, bottom - 8f, header);
        Center(c, "Unit", x.Unit + UnitW / 2f, bottom - 8f, header);

        TierHeader(c, q.Tier1Label, q.Currency, x.T1, y, middle, header, sub);
        TierHeader(c, q.Tier2Label, q.Currency, x.T2, y, middle, header, sub);
        TierHeader(c, q.Tier3Label, q.Currency, x.T3, y, middle, header, sub);

        foreach (var vx in Verticals(x))
            c.DrawLine(vx, y, vx, bottom, light);

        return bottom;
    }

    private static void TierHeader(SKCanvas c, string label, string currency, float x, float y, float middle, SKPaint header, SKPaint sub)
    {
        Center(c, string.IsNullOrWhiteSpace(label) ? "Tier" : label, x + TierW / 2f, y + 12f, header);
        Center(c, "Qty", x + QtyW / 2f, middle + 15f, sub);
        Center(c, "Unit Price", x + QtyW + PriceW / 2f, middle + 10f, sub);
        Center(c, $"({currency})", x + QtyW + PriceW / 2f, middle + 20f, sub);
        Center(c, "Amount", x + QtyW + PriceW + AmountW / 2f, middle + 15f, sub);
    }

    private static float ItemHeight(QuotationItem item)
    {
        using var detail = P(6.2f, Regular);
        var lines = DetailLines(item, detail);
        return Math.Max(24f, 22f + lines.Count * 8.4f);
    }

    private static void ItemRow(SKCanvas c, int n, QuotationItem item, float y, float height)
    {
        var x = Xs();
        using var product = P(7.1f, Bold);
        using var detail = P(6.2f, Regular);
        using var normal = P(6.8f, Regular);
        using var price = P(6.8f, item.DiscountPercent > 0 ? Bold : Regular);
        using var light = Stroke(0.35f, new SKColor(215, 215, 215));

        var baseY = y + 13f;
        Center(c, n.ToString(), x.Item + ItemW / 2f, baseY, normal);
        c.DrawText(item.ArticleName, x.Product + 4f, baseY, product);
        Center(c, string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit, x.Unit + UnitW / 2f, baseY, normal);

        TierValues(c, x.T1, item.Quantity, item.NetUnitPrice, item.LineTotal, item.Currency, baseY, normal, price);
        TierValues(c, x.T2, item.Quantity2, item.NetUnitPrice2, item.LineTotal2, item.Currency, baseY, normal, price);
        TierValues(c, x.T3, item.Quantity3, item.NetUnitPrice3, item.LineTotal3, item.Currency, baseY, normal, price);

        var dy = baseY + 10f;
        foreach (var text in DetailLines(item, detail))
        {
            c.DrawText(text, x.Product + 4f, dy, detail);
            dy += 8.4f;
        }

        c.DrawLine(x.Left, y + height, x.Right, y + height, light);
        foreach (var vx in Verticals(x))
            c.DrawLine(vx, y, vx, y + height, light);
    }

    private static void TierValues(SKCanvas c, float x, decimal qty, decimal unitPrice, decimal amount, string currency, float y, SKPaint normal, SKPaint price)
    {
        Center(c, Q(qty), x + QtyW / 2f, y, normal);
        Right(c, Money(unitPrice, currency), x + QtyW + PriceW - 4f, y, price);
        Right(c, Money(amount, currency), x + TierW - 4f, y, normal);
    }

    private static void Totals(SKCanvas c, Quotation q, float y)
    {
        var x = Xs();
        using var total = P(7.4f, Bold);
        using var note = P(6.5f, Italic);
        using var line = Stroke(0.9f, SKColors.Black);

        y += 8f;
        c.DrawLine(x.T1, y, x.Right, y, line);
        y += 16f;

        TierTotal(c, x.T1, q.Tier1Label, q.Tier1Total, q.Currency, y, total);
        TierTotal(c, x.T2, q.Tier2Label, q.Tier2Total, q.Currency, y, total);
        TierTotal(c, x.T3, q.Tier3Label, q.Tier3Total, q.Currency, y, total);

        y += 19f;
        c.DrawText("Note: NRE is a one-time charge and is included in each quantity-tier total above.", x.T1, y, note);

        if (!string.IsNullOrWhiteSpace(q.Remarks))
        {
            using var remarks = P(6.2f, Regular);
            y += 14f;
            foreach (var text in Wrap($"Remarks: {q.Remarks}", x.Right - x.T1, remarks).Take(3))
            {
                c.DrawText(text, x.T1, y, remarks);
                y += 8.4f;
            }
        }
    }

    private static void TierTotal(SKCanvas c, float x, string label, decimal amount, string currency, float y, SKPaint paint)
    {
        c.DrawText($"Total ({(string.IsNullOrWhiteSpace(label) ? "Tier" : label)})", x + 2f, y, paint);
        Right(c, Money(amount, currency), x + TierW - 4f, y, paint);
    }

    private static void Footer(SKCanvas c, bool bank)
    {
        using var bold = P(6.4f, Bold);
        using var text = P(6.1f, Regular);
        using var line = Stroke(0.65f, SKColors.Black);

        const float y = 544f;
        c.DrawLine(L, y - 7f, 540f, y - 7f, line);
        c.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", L, y + 4f, bold);
        c.DrawText("2699 Xiangyang North Street", L, y + 16f, text);
        c.DrawText("071000 Baoding", L, y + 28f, text);
        c.DrawText("China", L, y + 40f, text);

        if (!bank) return;

        Meta(c, "Bank Name:", "China Construction Bank", 280f, 346f, y + 4f, bold, text);
        Meta(c, "Bank Address:", "345 Longxing West Rd, Baoding, China", 280f, 346f, y + 16f, bold, text);
        Meta(c, "Bank Account:", "1301 4600 6002 2010 0241", 280f, 346f, y + 28f, bold, text);
        Meta(c, "Swift Code:", "PCBCCNBJ", 280f, 346f, y + 40f, bold, text);
    }

    private static List<string> DetailLines(QuotationItem item, SKPaint paint)
    {
        var result = new List<string>();
        result.AddRange(Wrap(item.Description, ProductW - 8f, paint));
        result.AddRange(Wrap(item.Specification, ProductW - 8f, paint));
        if (item.DiscountPercent > 0)
            result.Add($"Discount: {item.DiscountPercent:0.##}%");
        return result;
    }

    private static List<string> Wrap(string? text, float max, SKPaint paint)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            var current = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var next = current + " " + words[i];
                if (paint.MeasureText(next) <= max)
                    current = next;
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

    private static string Validity(Quotation q)
    {
        if (q.ValidUntil is null) return string.Empty;
        var days = (q.ValidUntil.Value.Date - q.QuotationDate.Date).Days;
        return days >= 0 ? $"{days} days" : q.ValidUntil.Value.ToString("dd.MM.yyyy");
    }

    private static string Money(decimal value, string currency)
        => currency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? $"${value:N2}" : $"CNY {value:N2}";

    private static string Q(decimal value)
        => value == decimal.Truncate(value) ? value.ToString("0") : value.ToString("0.####");

    private static void Meta(SKCanvas c, string label, string value, float lx, float vx, float y, SKPaint lp, SKPaint vp)
    {
        c.DrawText(label, lx, y, lp);
        c.DrawText(value ?? string.Empty, vx, y, vp);
    }

    private static void Center(SKCanvas c, string text, float x, float y, SKPaint p)
        => c.DrawText(text, x - p.MeasureText(text) / 2f, y, p);

    private static void Right(SKCanvas c, string text, float x, float y, SKPaint p)
        => c.DrawText(text, x - p.MeasureText(text), y, p);

    private static SKPaint P(float size, SKTypeface face) => new()
    {
        IsAntialias = true,
        Color = SKColors.Black,
        TextSize = size,
        Typeface = face
    };

    private static SKPaint Stroke(float width, SKColor color) => new()
    {
        IsAntialias = true,
        Color = color,
        StrokeWidth = width,
        Style = SKPaintStyle.Stroke
    };

    private static SKTypeface Typeface(SKFontStyle style)
    {
        foreach (var family in new[] { "Arial", "Liberation Sans", "DejaVu Sans" })
        {
            var result = SKTypeface.FromFamilyName(family, style);
            if (result is not null) return result;
        }
        return SKTypeface.Default;
    }

    private static (float Left, float Item, float Product, float Unit, float T1, float T2, float T3, float Right) Xs()
    {
        var item = L;
        var product = item + ItemW;
        var unit = product + ProductW;
        var t1 = unit + UnitW;
        var t2 = t1 + TierW;
        var t3 = t2 + TierW;
        return (L, item, product, unit, t1, t2, t3, t3 + TierW);
    }

    private static IEnumerable<float> Verticals((float Left, float Item, float Product, float Unit, float T1, float T2, float T3, float Right) x)
    {
        yield return x.Left;
        yield return x.Product;
        yield return x.Unit;
        yield return x.T1;
        yield return x.T1 + QtyW;
        yield return x.T1 + QtyW + PriceW;
        yield return x.T2;
        yield return x.T2 + QtyW;
        yield return x.T2 + QtyW + PriceW;
        yield return x.T3;
        yield return x.T3 + QtyW;
        yield return x.T3 + QtyW + PriceW;
        yield return x.Right;
    }
}
