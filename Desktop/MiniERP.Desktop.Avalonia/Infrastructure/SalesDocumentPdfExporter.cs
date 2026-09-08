using MiniERP.Domain;
using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

public static class SalesDocumentPdfExporter
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Left = 48f;
    private const float Right = 547f;
    private const float Bottom = 790f;
    private const float ContentBottom = 748f;

    private static readonly SKTypeface Regular = FindTypeface(SKFontStyle.Normal);
    private static readonly SKTypeface Bold = FindTypeface(SKFontStyle.Bold);

    public static void ExportInvoice(Invoice invoice, Stream output)
    {
        if (invoice.Items.Count == 0)
            throw new InvalidOperationException("The invoice needs at least one item before it can be exported.");

        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        var items = invoice.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        var index = 0;
        var page = 1;

        while (index < items.Count)
        {
            var canvas = document.BeginPage(PageWidth, PageHeight);
            canvas.Clear(SKColors.White);
            var y = DrawHeader(canvas, invoice.DocumentTitle, invoice.InvoiceNumber, invoice.InvoiceDate, page);

            if (page == 1)
                y = DrawInvoiceParties(canvas, invoice, y);

            y = DrawInvoiceTableHeader(canvas, y);

            // Keep enough room on the final page for total, bank information and remarks.
            // A few extra pages are preferable to allowing terms to run into the footer.
            var maxRows = page == 1 ? 10 : 14;
            var take = Math.Min(maxRows, items.Count - index);
            for (var row = 0; row < take; row++)
            {
                DrawInvoiceRow(canvas, index + row + 1, items[index + row], y);
                y += 30f;
            }

            index += take;
            if (index == items.Count)
            {
                y += 7f;
                DrawRightValue(canvas, "Total", $"{invoice.Currency} {invoice.TotalAmount:N2}", y, 10f, 11f);
                y += 28f;
                DrawInvoiceTerms(canvas, invoice, y);
            }
            else
            {
                using var continued = TextPaint(8f, Regular, SKColors.DimGray);
                canvas.DrawText("Continued on next page...", Left, y + 14f, continued);
            }

            DrawFooter(canvas, page);
            document.EndPage();
            page++;
        }

        document.Close();
    }

    public static void ExportPackingList(PackingList packingList, Stream output)
    {
        if (packingList.Items.Count == 0)
            throw new InvalidOperationException("The packing list needs at least one item before it can be exported.");
        if (packingList.Packages.Count == 0)
            throw new InvalidOperationException("The packing list needs at least one package/carton row before it can be exported.");

        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        var items = packingList.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        var packages = packingList.Packages.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToList();
        var itemIndex = 0;
        var packageIndex = 0;
        var page = 1;

        var canvas = BeginPackingPage(document, packingList, page, includeParties: true, out var y);
        using var section = TextPaint(10f, Bold);

        canvas.DrawText("Goods", Left, y, section);
        y += 12f;
        y = DrawPackingItemHeader(canvas, y);

        while (itemIndex < items.Count)
        {
            if (y + 26f > ContentBottom)
            {
                DrawContinued(canvas, y);
                EndPackingPage(document, canvas, page);
                page++;
                canvas = BeginPackingPage(document, packingList, page, includeParties: false, out y);
                canvas.DrawText("Goods — continued", Left, y, section);
                y += 12f;
                y = DrawPackingItemHeader(canvas, y);
            }

            DrawPackingItemRow(canvas, items[itemIndex], y);
            y += 26f;
            itemIndex++;
        }

        // Start the carton section on the same page when there is enough useful room;
        // otherwise move it cleanly to a new page.
        if (y + 82f > ContentBottom)
        {
            EndPackingPage(document, canvas, page);
            page++;
            canvas = BeginPackingPage(document, packingList, page, includeParties: false, out y);
        }
        else
        {
            y += 12f;
        }

        canvas.DrawText("Packages / Cartons", Left, y, section);
        y += 12f;
        y = DrawPackageHeader(canvas, y);

        while (packageIndex < packages.Count)
        {
            if (y + 24f > ContentBottom)
            {
                DrawContinued(canvas, y);
                EndPackingPage(document, canvas, page);
                page++;
                canvas = BeginPackingPage(document, packingList, page, includeParties: false, out y);
                canvas.DrawText("Packages / Cartons — continued", Left, y, section);
                y += 12f;
                y = DrawPackageHeader(canvas, y);
            }

            DrawPackageRow(canvas, packages[packageIndex], y);
            y += 24f;
            packageIndex++;
        }

        // Keep the totals together. If the last carton lands too close to the footer,
        // give the summary its own continuation page rather than clipping it.
        if (y + 34f > ContentBottom)
        {
            EndPackingPage(document, canvas, page);
            page++;
            canvas = BeginPackingPage(document, packingList, page, includeParties: false, out y);
        }

        y += 13f;
        using var total = TextPaint(9f, Bold);
        canvas.DrawText(
            $"Total Qty: {packingList.TotalQuantity:0.####}    Cartons: {packingList.TotalCartons}    N.W.: {packingList.TotalNetWeight:0.###} kg    G.W.: {packingList.TotalGrossWeight:0.###} kg    CBM: {packingList.TotalCbm:0.####}",
            Left,
            y,
            total);

        EndPackingPage(document, canvas, page);
        document.Close();
    }

    private static SKCanvas BeginPackingPage(SKDocument document, PackingList packingList, int page, bool includeParties, out float y)
    {
        var canvas = document.BeginPage(PageWidth, PageHeight);
        canvas.Clear(SKColors.White);
        y = DrawHeader(canvas, "Packing List", packingList.PackingListNumber, packingList.PackingDate, page);
        if (includeParties)
            y = DrawPackingParties(canvas, packingList, y);
        return canvas;
    }

    private static void EndPackingPage(SKDocument document, SKCanvas canvas, int page)
    {
        DrawFooter(canvas, page);
        document.EndPage();
    }

    private static void DrawContinued(SKCanvas canvas, float y)
    {
        using var continued = TextPaint(8f, Regular, SKColors.DimGray);
        canvas.DrawText("Continued on next page...", Left, Math.Min(y + 14f, ContentBottom + 6f), continued);
    }

    private static float DrawHeader(SKCanvas canvas, string title, string number, DateTime date, int page)
    {
        var blue = new SKColor(0, 108, 181);
        using var brand = TextPaint(19f, Bold, blue);
        using var company = TextPaint(9f, Bold);
        using var small = TextPaint(7f, Regular, SKColors.DimGray);
        canvas.DrawText("FORLINX", Left, 62f, brand);
        RightText(canvas, "Baoding Forlinx Embedded Technology Co., Ltd", Right, 54f, company);
        RightText(canvas, "Trusted Designer & Manufacturer of System on Module", Right, 67f, small);
        using var rule = Stroke(0.8f, SKColors.DimGray);
        canvas.DrawLine(Left, 82f, Right, 82f, rule);

        using var titlePaint = TextPaint(22f, Bold);
        RightText(canvas, title, Right, 118f, titlePaint);
        using var meta = TextPaint(8.5f, Regular);
        canvas.DrawText($"No.: {number}", Left, 115f, meta);
        canvas.DrawText($"Date: {date:MM.dd.yyyy}", Left, 129f, meta);
        if (page > 1) canvas.DrawText($"Page: {page}", Left, 143f, meta);
        return page == 1 ? 154f : 168f;
    }

    private static float DrawInvoiceParties(SKCanvas canvas, Invoice invoice, float y)
    {
        using var label = TextPaint(8.5f, Bold);
        using var text = TextPaint(8.3f, Regular);
        canvas.DrawText("Bill To", Left, y, label);
        canvas.DrawText(invoice.CustomerNameSnapshot ?? string.Empty, Left, y + 15f, label);
        var customerY = y + 29f;
        if (!string.IsNullOrWhiteSpace(invoice.CustomerContactSnapshot))
        {
            canvas.DrawText(invoice.CustomerContactSnapshot, Left, customerY, text);
            customerY += 13f;
        }
        foreach (var line in SplitLines(invoice.CustomerAddressSnapshot).Take(4))
        {
            canvas.DrawText(line, Left, customerY, text);
            customerY += 13f;
        }

        var metaX = 350f;
        DrawMeta(canvas, "PO No.", invoice.CustomerPoNumber, metaX, y, label, text);
        DrawMeta(canvas, "PO Date", invoice.CustomerPoDate?.ToString("MM.dd.yyyy"), metaX, y + 15f, label, text);
        DrawMeta(canvas, "Contact", invoice.SalesContactNameSnapshot, metaX, y + 30f, label, text);
        DrawMeta(canvas, "Payment", invoice.PaymentTerm, metaX, y + 45f, label, text);
        DrawMeta(canvas, "Delivery", invoice.DeliveryTerm, metaX, y + 60f, label, text);
        DrawMeta(canvas, "Source", invoice.SourceDocumentNumber, metaX, y + 75f, label, text);
        return Math.Max(customerY, y + 96f) + 8f;
    }

    private static float DrawPackingParties(SKCanvas canvas, PackingList packingList, float y)
    {
        using var label = TextPaint(8.5f, Bold);
        using var text = TextPaint(8.3f, Regular);
        canvas.DrawText("Ship To", Left, y, label);
        canvas.DrawText(packingList.CustomerNameSnapshot ?? string.Empty, Left, y + 15f, label);
        var customerY = y + 29f;
        if (!string.IsNullOrWhiteSpace(packingList.CustomerContactSnapshot))
        {
            canvas.DrawText(packingList.CustomerContactSnapshot, Left, customerY, text);
            customerY += 13f;
        }
        foreach (var line in SplitLines(packingList.CustomerAddressSnapshot).Take(4))
        {
            canvas.DrawText(line, Left, customerY, text);
            customerY += 13f;
        }
        var metaX = 350f;
        DrawMeta(canvas, "PO No.", packingList.CustomerPoNumber, metaX, y, label, text);
        DrawMeta(canvas, "PO Date", packingList.CustomerPoDate?.ToString("MM.dd.yyyy"), metaX, y + 15f, label, text);
        DrawMeta(canvas, "Contact", packingList.SalesContactNameSnapshot, metaX, y + 30f, label, text);
        DrawMeta(canvas, "Delivery", packingList.DeliveryTerm, metaX, y + 45f, label, text);
        DrawMeta(canvas, "Source", packingList.SourceDocumentNumber, metaX, y + 60f, label, text);
        return Math.Max(customerY, y + 82f) + 8f;
    }

    private static float DrawInvoiceTableHeader(SKCanvas canvas, float y)
    {
        DrawTableBand(canvas, y, 22f);
        using var p = TextPaint(8f, Bold, SKColors.White);
        canvas.DrawText("#", Left + 5f, y + 15f, p);
        canvas.DrawText("Product / Description", Left + 30f, y + 15f, p);
        RightText(canvas, "Qty", 350f, y + 15f, p);
        canvas.DrawText("Unit", 365f, y + 15f, p);
        RightText(canvas, "Unit Price", 459f, y + 15f, p);
        RightText(canvas, "Amount", Right - 5f, y + 15f, p);
        return y + 22f;
    }

    private static void DrawInvoiceRow(SKCanvas canvas, int number, InvoiceItem item, float y)
    {
        using var p = TextPaint(8f, Regular);
        using var bold = TextPaint(8f, Bold);
        canvas.DrawText(number.ToString(), Left + 5f, y + 18f, p);
        canvas.DrawText(Trim(item.ArticleName, 34), Left + 30f, y + 12f, bold);
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            using var small = TextPaint(6.7f, Regular, SKColors.DimGray);
            canvas.DrawText(Trim(item.Description, 48), Left + 30f, y + 24f, small);
        }
        RightText(canvas, item.Quantity.ToString("0.####"), 350f, y + 18f, p);
        canvas.DrawText(item.Unit, 365f, y + 18f, p);
        RightText(canvas, item.UnitPrice.ToString("N2"), 459f, y + 18f, p);
        RightText(canvas, item.LineTotal.ToString("N2"), Right - 5f, y + 18f, p);
        using var line = Stroke(0.35f, SKColors.LightGray);
        canvas.DrawLine(Left, y + 29f, Right, y + 29f, line);
    }

    private static float DrawPackingItemHeader(SKCanvas canvas, float y)
    {
        DrawTableBand(canvas, y, 22f);
        using var p = TextPaint(8f, Bold, SKColors.White);
        canvas.DrawText("Product / Description", Left + 5f, y + 15f, p);
        RightText(canvas, "Qty", 470f, y + 15f, p);
        canvas.DrawText("Unit", 490f, y + 15f, p);
        return y + 22f;
    }

    private static void DrawPackingItemRow(SKCanvas canvas, PackingListItem item, float y)
    {
        using var p = TextPaint(8f, Regular);
        using var bold = TextPaint(8f, Bold);
        canvas.DrawText(Trim(item.ArticleName, 50), Left + 5f, y + 11f, bold);
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            using var small = TextPaint(6.7f, Regular, SKColors.DimGray);
            canvas.DrawText(Trim(item.Description, 65), Left + 5f, y + 22f, small);
        }
        RightText(canvas, item.Quantity.ToString("0.####"), 470f, y + 15f, p);
        canvas.DrawText(item.Unit, 490f, y + 15f, p);
    }

    private static float DrawPackageHeader(SKCanvas canvas, float y)
    {
        DrawTableBand(canvas, y, 22f);
        using var p = TextPaint(7.4f, Bold, SKColors.White);
        canvas.DrawText("Carton", Left + 4f, y + 15f, p);
        canvas.DrawText("Count", 105f, y + 15f, p);
        canvas.DrawText("Contents", 145f, y + 15f, p);
        canvas.DrawText("L×W×H cm", 300f, y + 15f, p);
        canvas.DrawText("N.W.", 395f, y + 15f, p);
        canvas.DrawText("G.W.", 445f, y + 15f, p);
        canvas.DrawText("CBM", 500f, y + 15f, p);
        return y + 22f;
    }

    private static void DrawPackageRow(SKCanvas canvas, PackingPackage pck, float y)
    {
        using var p = TextPaint(7.2f, Regular);
        canvas.DrawText(Trim(pck.CartonNumber, 13), Left + 4f, y + 15f, p);
        canvas.DrawText(pck.PackageCount.ToString(), 105f, y + 15f, p);
        canvas.DrawText(Trim(pck.Contents, 22), 145f, y + 15f, p);
        canvas.DrawText($"{pck.LengthCm:0.##}×{pck.WidthCm:0.##}×{pck.HeightCm:0.##}", 300f, y + 15f, p);
        canvas.DrawText($"{pck.NetWeightKg:0.###}", 395f, y + 15f, p);
        canvas.DrawText($"{pck.GrossWeightKg:0.###}", 445f, y + 15f, p);
        canvas.DrawText($"{pck.Cbm:0.####}", 500f, y + 15f, p);
        using var line = Stroke(0.35f, SKColors.LightGray);
        canvas.DrawLine(Left, y + 23f, Right, y + 23f, line);
    }

    private static void DrawInvoiceTerms(SKCanvas canvas, Invoice invoice, float y)
    {
        using var label = TextPaint(8.5f, Bold);
        using var text = TextPaint(8f, Regular);
        if (!string.IsNullOrWhiteSpace(invoice.BankInformation))
        {
            canvas.DrawText("Bank Information", Left, y, label);
            var yy = y + 14f;
            foreach (var line in SplitLines(invoice.BankInformation).Take(5))
            {
                canvas.DrawText(line, Left, yy, text);
                yy += 12f;
            }
            y = yy + 5f;
        }
        if (!string.IsNullOrWhiteSpace(invoice.Remarks))
        {
            canvas.DrawText("Remarks", Left, y, label);
            var yy = y + 14f;
            foreach (var line in SplitLines(invoice.Remarks).Take(4))
            {
                canvas.DrawText(line, Left, yy, text);
                yy += 12f;
            }
        }
    }

    private static void DrawMeta(SKCanvas canvas, string label, string? value, float x, float y, SKPaint labelPaint, SKPaint valuePaint)
    {
        canvas.DrawText(label, x, y, labelPaint);
        canvas.DrawText(value ?? string.Empty, x + 62f, y, valuePaint);
    }

    private static void DrawRightValue(SKCanvas canvas, string label, string value, float y, float labelSize, float valueSize)
    {
        using var l = TextPaint(labelSize, Bold);
        using var v = TextPaint(valueSize, Bold);
        RightText(canvas, label, 450f, y, l);
        RightText(canvas, value, Right, y, v);
    }

    private static void DrawTableBand(SKCanvas canvas, float y, float height)
    {
        using var fill = new SKPaint { IsAntialias = true, Color = new SKColor(55, 75, 90), Style = SKPaintStyle.Fill };
        canvas.DrawRect(Left, y, Right - Left, height, fill);
    }

    private static void DrawFooter(SKCanvas canvas, int page)
    {
        using var rule = Stroke(0.5f, SKColors.Gray);
        canvas.DrawLine(Left, Bottom - 20f, Right, Bottom - 20f, rule);
        using var p = TextPaint(7f, Regular, SKColors.DimGray);
        canvas.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", Left, Bottom - 7f, p);
        RightText(canvas, $"Page {page}", Right, Bottom - 7f, p);
    }

    private static SKPaint TextPaint(float size, SKTypeface typeface, SKColor? color = null)
        => new() { IsAntialias = true, TextSize = size, Typeface = typeface, Color = color ?? SKColors.Black };

    private static SKPaint Stroke(float width, SKColor color)
        => new() { IsAntialias = true, Color = color, StrokeWidth = width, Style = SKPaintStyle.Stroke };

    private static void RightText(SKCanvas canvas, string text, float right, float y, SKPaint paint)
        => canvas.DrawText(text, right - paint.MeasureText(text), y, paint);

    private static IEnumerable<string> SplitLines(string? value)
        => (value ?? string.Empty).Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Trim(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..Math.Max(1, max - 1)] + "…";
    }

    private static SKTypeface FindTypeface(SKFontStyle style)
        => SKTypeface.FromFamilyName("DejaVu Sans", style)
           ?? SKTypeface.FromFamilyName(null, style)
           ?? SKTypeface.Default;
}
