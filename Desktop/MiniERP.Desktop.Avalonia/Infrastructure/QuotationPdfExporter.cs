using SkiaSharp;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

public static class QuotationPdfExporter
{
    private const float PageWidth = 842f;   // A4 landscape, points
    private const float PageHeight = 595f;
    private const float MarginLeft = 24f;
    private const float MarginRight = 24f;
    private const float FooterTop = 535f;

    private static readonly SKTypeface RegularTypeface = FindTypeface(SKFontStyle.Normal);
    private static readonly SKTypeface BoldTypeface = FindTypeface(SKFontStyle.Bold);
    private static readonly SKTypeface ItalicTypeface = FindTypeface(SKFontStyle.Italic);

    public static void Export(Quotation quotation, Stream output)
    {
        if (quotation.Items.Count == 0)
            throw new InvalidOperationException("A quotation needs at least one item before it can be exported.");

        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        var canvas = document.BeginPage(PageWidth, PageHeight);
        var y = DrawFirstPageHeader(canvas, quotation);
        y = DrawTableHeader(canvas, quotation, y);

        var itemNumber = 1;
        foreach (var item in quotation.Items)
        {
            var rowHeight = MeasureItemHeight(item);

            if (y + rowHeight > FooterTop)
            {
                DrawFooter(canvas, includeBankDetails: false);
                document.EndPage();

                canvas = document.BeginPage(PageWidth, PageHeight);
                y = DrawContinuationHeader(canvas);
                y = DrawTableHeader(canvas, quotation, y);
            }

            DrawItemRow(canvas, itemNumber++, item, y, rowHeight);
            y += rowHeight;
        }

        const float totalsRequiredHeight = 74f;
        if (y + totalsRequiredHeight > FooterTop)
        {
            DrawFooter(canvas, includeBankDetails: false);
            document.EndPage();

            canvas = document.BeginPage(PageWidth, PageHeight);
            y = DrawContinuationHeader(canvas);
            y += 10f;
        }

        DrawTotals(canvas, quotation, y);
        DrawFooter(canvas, includeBankDetails: true);

        document.EndPage();
        document.Close();
    }

    private static float DrawFirstPageHeader(SKCanvas canvas, Quotation quotation)
    {
        DrawCompanyHeader(canvas, 18f);

        using var title = Paint(19f, BoldTypeface);
        DrawCentered(canvas, "Quotation", PageWidth / 2f, 83f, title);

        using var customerName = Paint(8.5f, BoldTypeface);
        using var small = Paint(7.3f, RegularTypeface);
        using var label = Paint(7.5f, BoldTypeface);
        using var value = Paint(7.5f, RegularTypeface);

        var customer = FirstNonEmpty(quotation.CustomerNameSnapshot, quotation.Customer?.Name) ?? string.Empty;
        canvas.DrawText(customer, MarginLeft, 112f, customerName);

        var customerContact = FirstNonEmpty(quotation.CustomerContactSnapshot, string.Empty);
        if (!string.IsNullOrWhiteSpace(customerContact))
            canvas.DrawText(customerContact, MarginLeft, 126f, small);

        // The original template keeps the customer block intentionally compact.
        // If no contact is available, use the first line of the snapshotted address.
        if (string.IsNullOrWhiteSpace(customerContact) && !string.IsNullOrWhiteSpace(quotation.CustomerAddressSnapshot))
        {
            var firstAddressLine = quotation.CustomerAddressSnapshot
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstAddressLine))
                canvas.DrawText(firstAddressLine, MarginLeft, 126f, small);
        }

        var metaLabelX = 320f;
        var metaValueX = 405f;
        var metaY = 108f;
        const float metaStep = 12.2f;

        DrawMeta(canvas, "Date", quotation.QuotationDate.ToString("dd.MM.yyyy"), metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Quotation No.", quotation.QuotationNumber, metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Contact", FirstNonEmpty(quotation.SalesContactNameSnapshot, quotation.User?.Name) ?? string.Empty, metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Phone", FirstNonEmpty(quotation.SalesContactPhoneSnapshot, quotation.User?.Phone) ?? string.Empty, metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Email", FirstNonEmpty(quotation.SalesContactEmailSnapshot, quotation.User?.Email) ?? string.Empty, metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Payment Term", quotation.PaymentTerm ?? string.Empty, metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Delivery Term", quotation.DeliveryTerm ?? string.Empty, metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Lead Time", quotation.LeadTime ?? string.Empty, metaLabelX, metaValueX, metaY, label, value);
        metaY += metaStep;
        DrawMeta(canvas, "Validity", FormatValidity(quotation), metaLabelX, metaValueX, metaY, label, value);

        using var term = Paint(6.8f, RegularTypeface);
        var paymentText = string.IsNullOrWhiteSpace(quotation.PaymentTerm)
            ? "Payment term:"
            : $"Payment term: {quotation.PaymentTerm.Trim()}";

        canvas.DrawText(paymentText, MarginLeft, 213f, term);
        canvas.DrawText("Warranty: see attached warranty document", MarginLeft, 227f, term);

        return 244f;
    }

    private static float DrawContinuationHeader(SKCanvas canvas)
    {
        DrawCompanyHeader(canvas, 18f);
        return 66f;
    }

    private static void DrawCompanyHeader(SKCanvas canvas, float top)
    {
        using var company = Paint(10.5f, BoldTypeface);
        using var tagline = Paint(6.5f, ItalicTypeface);
        using var rule = LinePaint(0.8f, SKColors.Black);

        DrawCentered(canvas, "Baoding Forlinx Embedded Technology Co., Ltd", PageWidth / 2f, top + 2f, company);
        DrawCentered(canvas, "Trusted Designer & Manufacturer of System on Module", PageWidth / 2f, top + 14f, tagline);
        canvas.DrawLine(MarginLeft, top + 28f, PageWidth - MarginRight, top + 28f, rule);
    }

    private static float DrawTableHeader(SKCanvas canvas, Quotation quotation, float y)
    {
        var layout = TableLayout.Create();
        using var header = Paint(7.2f, BoldTypeface);
        using var sub = Paint(6.7f, BoldTypeface);
        using var line = LinePaint(0.8f, SKColors.Black);
        using var light = LinePaint(0.35f, new SKColor(205, 205, 205));

        var groupHeight = 17f;
        var subHeight = 25f;
        var top = y;
        var middle = y + groupHeight;
        var bottom = middle + subHeight;

        canvas.DrawLine(layout.Left, top, layout.Right, top, line);
        canvas.DrawLine(layout.Left, middle, layout.Right, middle, light);
        canvas.DrawLine(layout.Left, bottom, layout.Right, bottom, line);

        DrawCentered(canvas, "Item", layout.ItemCenter, bottom - 8f, header);
        DrawCentered(canvas, "Product", layout.ProductCenter, bottom - 8f, header);
        DrawCentered(canvas, "Unit", layout.UnitCenter, bottom - 8f, header);

        DrawCentered(canvas, Fallback(quotation.Tier1Label, "100 Sets"), layout.Tier1Center, y + 12f, header);
        DrawCentered(canvas, Fallback(quotation.Tier2Label, "500 Sets"), layout.Tier2Center, y + 12f, header);
        DrawCentered(canvas, Fallback(quotation.Tier3Label, "1000 Sets"), layout.Tier3Center, y + 12f, header);

        DrawTierSubHeader(canvas, layout.Tier1Start, quotation.Currency, middle, sub);
        DrawTierSubHeader(canvas, layout.Tier2Start, quotation.Currency, middle, sub);
        DrawTierSubHeader(canvas, layout.Tier3Start, quotation.Currency, middle, sub);

        foreach (var x in layout.VerticalRules)
            canvas.DrawLine(x, top, x, bottom, light);

        return bottom;
    }

    private static void DrawTierSubHeader(SKCanvas canvas, float tierStart, string currency, float top, SKPaint paint)
    {
        const float qtyWidth = 39f;
        const float priceWidth = 62f;
        const float amountWidth = 76f;

        DrawCentered(canvas, "Qty", tierStart + qtyWidth / 2f, top + 15f, paint);
        DrawCentered(canvas, "Unit Price", tierStart + qtyWidth + priceWidth / 2f, top + 10f, paint);
        DrawCentered(canvas, $"({currency})", tierStart + qtyWidth + priceWidth / 2f, top + 20f, paint);
        DrawCentered(canvas, "Amount", tierStart + qtyWidth + priceWidth + amountWidth / 2f, top + 15f, paint);
    }

    private static float MeasureItemHeight(QuotationItem item)
    {
        using var detail = Paint(6.2f, RegularTypeface);
        var productWidth = TableLayout.ProductWidth - 8f;

        var detailLines = new List<string>();
        detailLines.AddRange(WrapMultiline(item.Description, productWidth, detail));
        detailLines.AddRange(WrapMultiline(item.Specification, productWidth, detail));

        if (item.DiscountPercent > 0m)
            detailLines.Add($"Discount: {item.DiscountPercent:0.##}%");

        var detailHeight = detailLines.Count * 8.4f;
        return Math.Max(24f, 17f + detailHeight + 5f);
    }

    private static void DrawItemRow(SKCanvas canvas, int itemNumber, QuotationItem item, float y, float height)
    {
        var layout = TableLayout.Create();
        using var product = Paint(7.1f, BoldTypeface);
        using var detail = Paint(6.2f, RegularTypeface);
        using var normal = Paint(6.8f, RegularTypeface);
        using var emphasizedMoney = Paint(6.8f, item.DiscountPercent > 0m ? BoldTypeface : RegularTypeface);
        using var light = LinePaint(0.35f, new SKColor(215, 215, 215));

        var baseline = y + 13f;
        DrawCentered(canvas, itemNumber.ToString(), layout.ItemCenter, baseline, normal);
        canvas.DrawText(item.ArticleName, layout.ProductStart + 4f, baseline, product);
        DrawCentered(canvas, string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit, layout.UnitCenter, baseline, normal);

        DrawTierValues(canvas, layout.Tier1Start, item.Quantity, item.NetUnitPrice, item.LineTotal, item.Currency, baseline, normal, emphasizedMoney);
        DrawTierValues(canvas, layout.Tier2Start, item.Quantity2, item.NetUnitPrice2, item.LineTotal2, item.Currency, baseline, normal, emphasizedMoney);
        DrawTierValues(canvas, layout.Tier3Start, item.Quantity3, item.NetUnitPrice3, item.LineTotal3, item.Currency, baseline, normal, emphasizedMoney);

        var detailY = baseline + 10f;
        var detailLines = new List<string>();
        detailLines.AddRange(WrapMultiline(item.Description, layout.ProductWidth - 8f, detail));
        detailLines.AddRange(WrapMultiline(item.Specification, layout.ProductWidth - 8f, detail));

        if (item.DiscountPercent > 0m)
            detailLines.Add($"Discount: {item.DiscountPercent:0.##}%");

        foreach (var lineText in detailLines)
        {
            canvas.DrawText(lineText, layout.ProductStart + 4f, detailY, detail);
            detailY += 8.4f;
        }

        canvas.DrawLine(layout.Left, y + height, layout.Right, y + height, light);

        foreach (var x in layout.VerticalRules)
            canvas.DrawLine(x, y, x, y + height, light);
    }

    private static void DrawTierValues(
        SKCanvas canvas,
        float tierStart,
        decimal quantity,
        decimal unitPrice,
        decimal amount,
        string currency,
        float baseline,
        SKPaint normal,
        SKPaint money)
    {
        const float qtyWidth = 39f;
        const float priceWidth = 62f;
        const float amountWidth = 76f;

        DrawCentered(canvas, FormatQuantity(quantity), tierStart + qtyWidth / 2f, baseline, normal);
        DrawRight(canvas, FormatMoney(unitPrice, currency), tierStart + qtyWidth + priceWidth - 4f, baseline, money);
        DrawRight(canvas, FormatMoney(amount, currency), tierStart + qtyWidth + priceWidth + amountWidth - 4f, baseline, normal);
    }

    private static void DrawTotals(SKCanvas canvas, Quotation quotation, float y)
    {
        var layout = TableLayout.Create();
        using var total = Paint(7.4f, BoldTypeface);
        using var note = Paint(6.5f, ItalicTypeface);
        using var line = LinePaint(0.9f, SKColors.Black);

        y += 8f;
        canvas.DrawLine(layout.Tier1Start, y, layout.Right, y, line);
        y += 16f;

        DrawTierTotal(canvas, layout.Tier1Start, Fallback(quotation.Tier1Label, "100 Sets"), quotation.Tier1Total, quotation.Currency, y, total);
        DrawTierTotal(canvas, layout.Tier2Start, Fallback(quotation.Tier2Label, "500 Sets"), quotation.Tier2Total, quotation.Currency, y, total);
        DrawTierTotal(canvas, layout.Tier3Start, Fallback(quotation.Tier3Label, "1000 Sets"), quotation.Tier3Total, quotation.Currency, y, total);

        y += 19f;
        canvas.DrawText(
            "Note: NRE is a one-time charge and is included in each quantity-tier total above.",
            layout.Tier1Start,
            y,
            note);

        if (!string.IsNullOrWhiteSpace(quotation.Remarks))
        {
            y += 14f;
            using var remarks = Paint(6.2f, RegularTypeface);
            var wrapped = WrapMultiline($"Remarks: {quotation.Remarks}", layout.Right - layout.Tier1Start, remarks);
            foreach (var lineText in wrapped.Take(3))
            {
                canvas.DrawText(lineText, layout.Tier1Start, y, remarks);
                y += 8.4f;
            }
        }
    }

    private static void DrawTierTotal(
        SKCanvas canvas,
        float tierStart,
        string label,
        decimal amount,
        string currency,
        float baseline,
        SKPaint paint)
    {
        var text = $"Total ({label})";
        canvas.DrawText(text, tierStart + 2f, baseline, paint);
        DrawRight(canvas, FormatMoney(amount, currency), tierStart + TableLayout.TierWidth - 4f, baseline, paint);
    }

    private static void DrawFooter(SKCanvas canvas, bool includeBankDetails)
    {
        using var heading = Paint(6.4f, BoldTypeface);
        using var text = Paint(6.1f, RegularTypeface);
        using var line = LinePaint(0.65f, SKColors.Black);

        var y = 544f;
        canvas.DrawLine(MarginLeft, y - 7f, 540f, y - 7f, line);

        canvas.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", MarginLeft, y + 4f, heading);
        canvas.DrawText("2699 Xiangyang North Street", MarginLeft, y + 16f, text);
        canvas.DrawText("071000 Baoding", MarginLeft, y + 28f, text);
        canvas.DrawText("China", MarginLeft, y + 40f, text);

        if (!includeBankDetails)
            return;

        var labelX = 280f;
        var valueX = 346f;

        DrawMeta(canvas, "Bank Name:", "China Construction Bank", labelX, valueX, y + 4f, heading, text);
        DrawMeta(canvas, "Bank Address:", "345 Longxing West Rd, Baoding, China", labelX, valueX, y + 16f, heading, text);
        DrawMeta(canvas, "Bank Account:", "1301 4600 6002 2010 0241", labelX, valueX, y + 28f, heading, text);
        DrawMeta(canvas, "Swift Code:", "PCBCCNBJ", labelX, valueX, y + 40f, heading, text);
    }

    private static void DrawMeta(
        SKCanvas canvas,
        string labelText,
        string valueText,
        float labelX,
        float valueX,
        float baseline,
        SKPaint labelPaint,
        SKPaint valuePaint)
    {
        canvas.DrawText(labelText, labelX, baseline, labelPaint);
        canvas.DrawText(valueText ?? string.Empty, valueX, baseline, valuePaint);
    }

    private static List<string> WrapMultiline(string? text, float maxWidth, SKPaint paint)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (var paragraph in text
                     .Replace("\r\n", "\n")
                     .Replace('\r', '\n')
                     .Split('\n'))
        {
            result.AddRange(WrapText(paragraph.Trim(), maxWidth, paint));
        }

        return result;
    }

    private static List<string> WrapText(string text, float maxWidth, SKPaint paint)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return result;

        var current = words[0];

        for (var i = 1; i < words.Length; i++)
        {
            var candidate = current + " " + words[i];
            if (paint.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            result.Add(current);
            current = words[i];
        }

        result.Add(current);
        return result;
    }

    private static string FormatMoney(decimal value, string currency)
        => string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase)
            ? $"${value:N2}"
            : $"CNY {value:N2}";

    private static string FormatQuantity(decimal value)
        => value == decimal.Truncate(value)
            ? value.ToString("0")
            : value.ToString("0.####");

    private static string FormatValidity(Quotation quotation)
    {
        if (quotation.ValidUntil is null)
            return string.Empty;

        var days = (quotation.ValidUntil.Value.Date - quotation.QuotationDate.Date).Days;
        return days >= 0 ? $"{days} days" : quotation.ValidUntil.Value.ToString("dd.MM.yyyy");
    }

    private static string Fallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? FirstNonEmpty(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;

    private static SKPaint Paint(float size, SKTypeface typeface)
        => new()
        {
            IsAntialias = true,
            Color = SKColors.Black,
            TextSize = size,
            Typeface = typeface
        };

    private static SKPaint LinePaint(float width, SKColor color)
        => new()
        {
            IsAntialias = true,
            Color = color,
            StrokeWidth = width,
            Style = SKPaintStyle.Stroke
        };

    private static void DrawCentered(SKCanvas canvas, string text, float centerX, float baseline, SKPaint paint)
        => canvas.DrawText(text, centerX - paint.MeasureText(text) / 2f, baseline, paint);

    private static void DrawRight(SKCanvas canvas, string text, float rightX, float baseline, SKPaint paint)
        => canvas.DrawText(text, rightX - paint.MeasureText(text), baseline, paint);

    private static SKTypeface FindTypeface(SKFontStyle style)
    {
        foreach (var family in new[] { "Arial", "Liberation Sans", "DejaVu Sans" })
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null)
                return typeface;
        }

        return SKTypeface.Default;
    }

    private readonly record struct TableLayout(
        float Left,
        float Right,
        float ItemStart,
        float ProductStart,
        float UnitStart,
        float Tier1Start,
        float Tier2Start,
        float Tier3Start)
    {
        public const float ItemWidth = 28f;
        public const float ProductWidth = 196f;
        public const float UnitWidth = 38f;
        public const float TierWidth = 177f;

        public float ItemCenter => ItemStart + ItemWidth / 2f;
        public float ProductCenter => ProductStart + ProductWidth / 2f;
        public float UnitCenter => UnitStart + UnitWidth / 2f;
        public float Tier1Center => Tier1Start + TierWidth / 2f;
        public float Tier2Center => Tier2Start + TierWidth / 2f;
        public float Tier3Center => Tier3Start + TierWidth / 2f;

        public IEnumerable<float> VerticalRules
        {
            get
            {
                yield return Left;
                yield return ProductStart;
                yield return UnitStart;
                yield return Tier1Start;
                yield return Tier1Start + 39f;
                yield return Tier1Start + 101f;
                yield return Tier2Start;
                yield return Tier2Start + 39f;
                yield return Tier2Start + 101f;
                yield return Tier3Start;
                yield return Tier3Start + 39f;
                yield return Tier3Start + 101f;
                yield return Right;
            }
        }

        public static TableLayout Create()
        {
            var itemStart = MarginLeft;
            var productStart = itemStart + ItemWidth;
            var unitStart = productStart + ProductWidth;
            var tier1Start = unitStart + UnitWidth;
            var tier2Start = tier1Start + TierWidth;
            var tier3Start = tier2Start + TierWidth;
            var right = tier3Start + TierWidth;

            return new TableLayout(
                MarginLeft,
                right,
                itemStart,
                productStart,
                unitStart,
                tier1Start,
                tier2Start,
                tier3Start);
        }
    }
}
