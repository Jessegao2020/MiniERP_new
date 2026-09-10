using MiniERP.Domain;
using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

public static class SalesDocumentPdfExporter
{
    private const float PageWidth = DocumentPdfStyle.PageWidth;
    private const float PageHeight = DocumentPdfStyle.PageHeight;
    private const float Left = DocumentPdfStyle.Left;
    private const float Right = DocumentPdfStyle.Right;
    private const float ContentBottom = 704f;

    private const float ItemCenterX = 72f;
    private const float ProductX = 88f;
    private const float ProductWidth = 215f;
    private const float UnitPriceRightX = 350f;
    private const float QuantityRightX = 411f;
    private const float UnitCenterX = 440f;
    private const float AmountRightX = 536f;

    private static readonly SKTypeface Regular = DocumentPdfStyle.Regular;
    private static readonly SKTypeface Bold = DocumentPdfStyle.Bold;
    private static readonly SKTypeface Italic = DocumentPdfStyle.Italic;

    public static void ExportInvoice(Invoice invoice, Stream output)
    {
        if (invoice.Items.Count == 0)
            throw new InvalidOperationException("The invoice needs at least one item before it can be exported.");

        var items = invoice.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        var pages = BuildInvoicePages(items, invoice);

        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = pages[pageIndex];
            var canvas = document.BeginPage(PageWidth, PageHeight);
            canvas.Clear(SKColors.White);

            var y = pageIndex == 0
                ? DrawInvoiceFirstPageHeader(canvas, invoice)
                : DrawContinuationHeader(canvas, invoice.DocumentTitle, invoice.InvoiceNumber);
            y = DrawInvoiceTableHeader(canvas, invoice.Currency, y);

            var itemNumber = page.StartItemNumber;
            foreach (var item in page.Items)
            {
                var rowHeight = MeasureInvoiceItemHeight(item);
                DrawInvoiceRow(canvas, itemNumber++, item, y, rowHeight);
                y += rowHeight;
            }

            using (var tableEnd = Stroke(0.8f, SKColors.Black))
                canvas.DrawLine(Left, y + 2f, Right, y + 2f, tableEnd);
            y += 7f;

            if (page.IsLast)
                DrawInvoiceClosing(canvas, invoice, y);
            else
                DocumentPdfStyle.DrawContinuedOnNextPage(canvas, y + 8f);

            DocumentPdfStyle.DrawFooter(canvas, pageIndex + 1, pages.Count);
            document.EndPage();
        }

        document.Close();
    }

    public static void ExportPackingList(PackingList packingList, Stream output)
    {
        if (packingList.Items.Count == 0)
            throw new InvalidOperationException("The packing list needs at least one item before it can be exported.");
        if (packingList.Packages.Count == 0)
            throw new InvalidOperationException("The packing list needs at least one package/carton row before it can be exported.");

        var items = packingList.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        var packages = packingList.Packages.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToList();
        var pageCount = CountPackingPages(items, packages);

        using var document = SKDocument.CreatePdf(output)
            ?? throw new InvalidOperationException("Could not create PDF document.");

        var itemIndex = 0;
        var packageIndex = 0;
        var pageNumber = 1;
        var canvas = BeginPackingPage(document, packingList, pageNumber, firstPage: true, out var y);

        using var section = Paint(9f, Bold);
        canvas.DrawText("Goods", Left, y, section);
        y += 12f;
        y = DrawPackingItemHeader(canvas, y);

        while (itemIndex < items.Count)
        {
            var rowHeight = MeasurePackingItemHeight(items[itemIndex]);
            if (y + rowHeight > ContentBottom)
            {
                DocumentPdfStyle.DrawContinuedOnNextPage(canvas, Math.Min(y + 8f, 716f));
                DocumentPdfStyle.DrawFooter(canvas, pageNumber, pageCount);
                document.EndPage();

                pageNumber++;
                canvas = BeginPackingPage(document, packingList, pageNumber, firstPage: false, out y);
                canvas.DrawText("Goods — continued", Left, y, section);
                y += 12f;
                y = DrawPackingItemHeader(canvas, y);
            }

            DrawPackingItemRow(canvas, itemIndex + 1, items[itemIndex], y, rowHeight);
            y += rowHeight;
            itemIndex++;
        }

        using (var goodsEnd = Stroke(0.8f, SKColors.Black))
            canvas.DrawLine(Left, y + 2f, Right, y + 2f, goodsEnd);
        y += 14f;

        if (y + 82f > ContentBottom)
        {
            DocumentPdfStyle.DrawFooter(canvas, pageNumber, pageCount);
            document.EndPage();
            pageNumber++;
            canvas = BeginPackingPage(document, packingList, pageNumber, firstPage: false, out y);
        }

        canvas.DrawText("Packages / Cartons", Left, y, section);
        y += 12f;
        y = DrawPackageHeader(canvas, y);

        while (packageIndex < packages.Count)
        {
            const float rowHeight = 24f;
            if (y + rowHeight > ContentBottom)
            {
                DocumentPdfStyle.DrawContinuedOnNextPage(canvas, Math.Min(y + 8f, 716f));
                DocumentPdfStyle.DrawFooter(canvas, pageNumber, pageCount);
                document.EndPage();

                pageNumber++;
                canvas = BeginPackingPage(document, packingList, pageNumber, firstPage: false, out y);
                canvas.DrawText("Packages / Cartons — continued", Left, y, section);
                y += 12f;
                y = DrawPackageHeader(canvas, y);
            }

            DrawPackageRow(canvas, packages[packageIndex], y);
            y += rowHeight;
            packageIndex++;
        }

        using (var packagesEnd = Stroke(0.8f, SKColors.Black))
            canvas.DrawLine(Left, y + 2f, Right, y + 2f, packagesEnd);
        y += 16f;

        if (y + 28f > ContentBottom)
        {
            DocumentPdfStyle.DrawFooter(canvas, pageNumber, pageCount);
            document.EndPage();
            pageNumber++;
            canvas = BeginPackingPage(document, packingList, pageNumber, firstPage: false, out y);
        }

        DrawPackingTotals(canvas, packingList, y);
        DocumentPdfStyle.DrawFooter(canvas, pageNumber, pageCount);
        document.EndPage();
        document.Close();
    }

    private static List<InvoicePagePlan> BuildInvoicePages(IReadOnlyList<InvoiceItem> items, Invoice invoice)
    {
        var pages = new List<InvoicePagePlan>();
        var current = new List<InvoiceItem>();
        var y = 354f;
        var currentStart = 1;
        var nextStart = 1;
        var closingHeight = MeasureInvoiceClosingHeight(invoice);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var height = MeasureInvoiceItemHeight(item);
            var isLastItem = index == items.Count - 1;
            var limit = isLastItem ? ContentBottom - closingHeight : 684f;

            if (current.Count > 0 && y + height > limit)
            {
                pages.Add(new InvoicePagePlan(currentStart, current.ToList(), false));
                nextStart += current.Count;
                currentStart = nextStart;
                current.Clear();
                y = 150f;
            }

            current.Add(item);
            y += height;
        }

        pages.Add(new InvoicePagePlan(currentStart, current.ToList(), true));
        return pages;
    }

    private static float DrawInvoiceFirstPageHeader(SKCanvas canvas, Invoice invoice)
    {
        DocumentPdfStyle.DrawBrandHeader(canvas, 71f, 108f);
        using var title = Paint(24f, Bold);
        RightText(canvas, invoice.DocumentTitle, Right - 3f, 146f, title);

        using var customerName = Paint(9.3f, Bold);
        using var customerText = Paint(8.4f, Regular);
        var customerY = 178f;
        canvas.DrawText(invoice.CustomerNameSnapshot ?? string.Empty, Left + 3f, customerY, customerName);
        customerY += 13f;

        if (!string.IsNullOrWhiteSpace(invoice.CustomerContactSnapshot))
        {
            canvas.DrawText(invoice.CustomerContactSnapshot, Left + 3f, customerY, customerText);
            customerY += 13f;
        }

        var printableAddress = CountryRegionNames.ExpandAddressCountry(invoice.CustomerAddressSnapshot);
        foreach (var line in SplitLines(printableAddress).Take(4))
        {
            canvas.DrawText(line, Left + 3f, customerY, customerText);
            customerY += 13f;
        }

        using var label = Paint(8.5f, Bold);
        using var value = Paint(8.3f, Regular);
        var metaY = 178f;
        const float step = 13f;
        const float labelX = 354f;
        const float valueX = 443f;

        DrawMeta(canvas, "Date", invoice.InvoiceDate.ToString("MM.dd.yyyy"), labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Invoice No.", invoice.InvoiceNumber, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Contact", invoice.SalesContactNameSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Phone", invoice.SalesContactPhoneSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Email", invoice.SalesContactEmailSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Payment Term", invoice.PaymentTerm ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Delivery Term", invoice.DeliveryTerm ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Customer PO", invoice.CustomerPoNumber ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "PO Date", invoice.CustomerPoDate?.ToString("MM.dd.yyyy") ?? string.Empty, labelX, valueX, metaY, label, value);

        return 317f;
    }

    private static float DrawPackingFirstPageHeader(SKCanvas canvas, PackingList packingList)
    {
        DocumentPdfStyle.DrawBrandHeader(canvas, 71f, 108f);
        using var title = Paint(24f, Bold);
        RightText(canvas, "Packing List", Right - 3f, 146f, title);

        using var customerName = Paint(9.3f, Bold);
        using var customerText = Paint(8.4f, Regular);
        var customerY = 178f;
        canvas.DrawText(packingList.CustomerNameSnapshot ?? string.Empty, Left + 3f, customerY, customerName);
        customerY += 13f;

        if (!string.IsNullOrWhiteSpace(packingList.CustomerContactSnapshot))
        {
            canvas.DrawText(packingList.CustomerContactSnapshot, Left + 3f, customerY, customerText);
            customerY += 13f;
        }

        var printableAddress = CountryRegionNames.ExpandAddressCountry(packingList.CustomerAddressSnapshot);
        foreach (var line in SplitLines(printableAddress).Take(4))
        {
            canvas.DrawText(line, Left + 3f, customerY, customerText);
            customerY += 13f;
        }

        using var label = Paint(8.5f, Bold);
        using var value = Paint(8.3f, Regular);
        var metaY = 178f;
        const float step = 13f;
        const float labelX = 354f;
        const float valueX = 443f;

        DrawMeta(canvas, "Date", packingList.PackingDate.ToString("MM.dd.yyyy"), labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Packing List No.", packingList.PackingListNumber, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Contact", packingList.SalesContactNameSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Phone", packingList.SalesContactPhoneSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Email", packingList.SalesContactEmailSnapshot ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Delivery Term", packingList.DeliveryTerm ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Customer PO", packingList.CustomerPoNumber ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "PO Date", packingList.CustomerPoDate?.ToString("MM.dd.yyyy") ?? string.Empty, labelX, valueX, metaY, label, value); metaY += step;
        DrawMeta(canvas, "Source", packingList.SourceDocumentNumber ?? string.Empty, labelX, valueX, metaY, label, value);

        return 317f;
    }

    private static float DrawContinuationHeader(SKCanvas canvas, string title, string number)
    {
        DocumentPdfStyle.DrawBrandHeader(canvas, 55f, 88f);
        using var small = Paint(10f, Bold);
        canvas.DrawText($"{title} {number} — continued", Left, 108f, small);
        return 116f;
    }

    private static float DrawInvoiceTableHeader(SKCanvas canvas, string currency, float y)
    {
        using var header = Paint(9f, Bold);
        using var currencyPaint = Paint(8f, Bold);
        using var line = Stroke(0.8f, new SKColor(70, 70, 70));

        DocumentPdfStyle.CenterText(canvas, $"({currency})", 337f, y + 3f, currencyPaint);
        var baseline = y + 17f;
        DocumentPdfStyle.CenterText(canvas, "Item", ItemCenterX, baseline, header);
        canvas.DrawText("Product", ProductX, baseline, header);
        RightText(canvas, "Unit Price", UnitPriceRightX, baseline, header);
        RightText(canvas, "Quantity", QuantityRightX, baseline, header);
        DocumentPdfStyle.CenterText(canvas, "Unit", UnitCenterX, baseline, header);
        RightText(canvas, "Amount", AmountRightX, baseline, header);
        canvas.DrawLine(Left, baseline + 5f, Right, baseline + 5f, line);
        return baseline + 17f;
    }

    private static float MeasureInvoiceItemHeight(InvoiceItem item)
    {
        using var detail = Paint(7.2f, Regular);
        var lines = WrapText(item.Description, ProductWidth, detail);
        return Math.Max(45f, 30f + lines.Count * 10f);
    }

    private static void DrawInvoiceRow(SKCanvas canvas, int number, InvoiceItem item, float y, float height)
    {
        using var name = Paint(9f, Bold);
        using var detail = Paint(7.2f, Regular);
        using var normal = Paint(8.5f, Regular);

        var baseline = y + 12f;
        DocumentPdfStyle.CenterText(canvas, number.ToString(), ItemCenterX, baseline, normal);
        canvas.DrawText(item.ArticleName, ProductX, baseline, name);

        var netPrice = decimal.Round(
            item.UnitPrice * (1m - item.DiscountPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);
        RightText(canvas, FormatMoney(netPrice, item.Currency), UnitPriceRightX, baseline, normal);
        RightText(canvas, FormatQuantity(item.Quantity), QuantityRightX, baseline, normal);
        DocumentPdfStyle.CenterText(canvas, string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit, UnitCenterX, baseline, normal);
        RightText(canvas, FormatMoney(item.LineTotal, item.Currency), AmountRightX, baseline, normal);

        var detailY = baseline + 14f;
        foreach (var line in WrapText(item.Description, ProductWidth, detail))
        {
            canvas.DrawText(line, ProductX, detailY, detail);
            detailY += 10f;
        }
    }

    private static void DrawInvoiceClosing(SKCanvas canvas, Invoice invoice, float y)
    {
        using var totalLabel = Paint(9f, Bold);
        using var totalAmount = Paint(9f, Bold);
        using var line = Stroke(0.8f, SKColors.Black);

        RightText(canvas, "Total", 438f, y + 15f, totalLabel);
        RightText(canvas, FormatMoney(invoice.TotalAmount, invoice.Currency), Right, y + 15f, totalAmount);
        canvas.DrawLine(350f, y + 20f, Right, y + 20f, line);
        y += 39f;

        using var heading = Paint(8.5f, Bold);
        using var text = Paint(7.6f, Regular);
        if (!string.IsNullOrWhiteSpace(invoice.BankInformation))
        {
            canvas.DrawText("Bank Information", Left, y, heading);
            y += 13f;
            foreach (var wrapped in WrapText(invoice.BankInformation, Right - Left, text).Take(5))
            {
                canvas.DrawText(wrapped, Left, y, text);
                y += 10f;
            }
            y += 5f;
        }

        if (!string.IsNullOrWhiteSpace(invoice.Remarks))
        {
            canvas.DrawText("Remarks", Left, y, heading);
            y += 13f;
            foreach (var wrapped in WrapText(invoice.Remarks, Right - Left, text).Take(4))
            {
                canvas.DrawText(wrapped, Left, y, text);
                y += 10f;
            }
        }
    }

    private static float MeasureInvoiceClosingHeight(Invoice invoice)
    {
        using var text = Paint(7.6f, Regular);
        var height = 39f;
        if (!string.IsNullOrWhiteSpace(invoice.BankInformation))
            height += 18f + WrapText(invoice.BankInformation, Right - Left, text).Take(5).Count() * 10f;
        if (!string.IsNullOrWhiteSpace(invoice.Remarks))
            height += 13f + WrapText(invoice.Remarks, Right - Left, text).Take(4).Count() * 10f;
        return height + 8f;
    }

    private static SKCanvas BeginPackingPage(
        SKDocument document,
        PackingList packingList,
        int pageNumber,
        bool firstPage,
        out float y)
    {
        var canvas = document.BeginPage(PageWidth, PageHeight);
        canvas.Clear(SKColors.White);
        y = firstPage
            ? DrawPackingFirstPageHeader(canvas, packingList)
            : DrawContinuationHeader(canvas, "Packing List", packingList.PackingListNumber);
        return canvas;
    }

    private static float DrawPackingItemHeader(SKCanvas canvas, float y)
    {
        using var header = Paint(9f, Bold);
        using var line = Stroke(0.8f, new SKColor(70, 70, 70));
        var baseline = y + 17f;
        DocumentPdfStyle.CenterText(canvas, "Item", ItemCenterX, baseline, header);
        canvas.DrawText("Product", ProductX, baseline, header);
        RightText(canvas, "Quantity", 470f, baseline, header);
        canvas.DrawText("Unit", 490f, baseline, header);
        canvas.DrawLine(Left, baseline + 5f, Right, baseline + 5f, line);
        return baseline + 17f;
    }

    private static float MeasurePackingItemHeight(PackingListItem item)
    {
        using var detail = Paint(7.2f, Regular);
        var lines = WrapText(item.Description, 310f, detail);
        return Math.Max(40f, 28f + lines.Count * 10f);
    }

    private static void DrawPackingItemRow(SKCanvas canvas, int number, PackingListItem item, float y, float height)
    {
        using var name = Paint(9f, Bold);
        using var detail = Paint(7.2f, Regular);
        using var normal = Paint(8.5f, Regular);

        var baseline = y + 12f;
        DocumentPdfStyle.CenterText(canvas, number.ToString(), ItemCenterX, baseline, normal);
        canvas.DrawText(item.ArticleName, ProductX, baseline, name);
        RightText(canvas, FormatQuantity(item.Quantity), 470f, baseline, normal);
        canvas.DrawText(string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit, 490f, baseline, normal);

        var detailY = baseline + 14f;
        foreach (var line in WrapText(item.Description, 310f, detail))
        {
            canvas.DrawText(line, ProductX, detailY, detail);
            detailY += 10f;
        }
    }

    private static float DrawPackageHeader(SKCanvas canvas, float y)
    {
        using var header = Paint(8f, Bold);
        using var line = Stroke(0.8f, new SKColor(70, 70, 70));
        var baseline = y + 17f;
        canvas.DrawText("Carton", Left + 2f, baseline, header);
        canvas.DrawText("Count", 112f, baseline, header);
        canvas.DrawText("Contents", 151f, baseline, header);
        canvas.DrawText("L×W×H cm", 302f, baseline, header);
        canvas.DrawText("N.W.", 394f, baseline, header);
        canvas.DrawText("G.W.", 443f, baseline, header);
        canvas.DrawText("CBM", 493f, baseline, header);
        canvas.DrawLine(Left, baseline + 5f, Right, baseline + 5f, line);
        return baseline + 17f;
    }

    private static void DrawPackageRow(SKCanvas canvas, PackingPackage package, float y)
    {
        using var text = Paint(7.6f, Regular);
        canvas.DrawText(Trim(package.CartonNumber, 13), Left + 2f, y + 14f, text);
        canvas.DrawText(package.PackageCount.ToString(), 112f, y + 14f, text);
        canvas.DrawText(Trim(package.Contents, 22), 151f, y + 14f, text);
        canvas.DrawText($"{package.LengthCm:0.##}×{package.WidthCm:0.##}×{package.HeightCm:0.##}", 302f, y + 14f, text);
        canvas.DrawText($"{package.NetWeightKg:0.###}", 394f, y + 14f, text);
        canvas.DrawText($"{package.GrossWeightKg:0.###}", 443f, y + 14f, text);
        canvas.DrawText($"{package.Cbm:0.####}", 493f, y + 14f, text);
    }

    private static void DrawPackingTotals(SKCanvas canvas, PackingList packingList, float y)
    {
        using var label = Paint(8.5f, Bold);
        using var value = Paint(8.3f, Regular);
        canvas.DrawText("Summary", Left, y, label);
        y += 14f;
        canvas.DrawText(
            $"Total Qty: {packingList.TotalQuantity:0.####}    Cartons: {packingList.TotalCartons}    " +
            $"N.W.: {packingList.TotalNetWeight:0.###} kg    G.W.: {packingList.TotalGrossWeight:0.###} kg    " +
            $"CBM: {packingList.TotalCbm:0.####}",
            Left,
            y,
            value);
    }

    private static int CountPackingPages(IReadOnlyList<PackingListItem> items, IReadOnlyList<PackingPackage> packages)
    {
        var pages = 1;
        var y = 317f + 12f;
        y = SimulateHeader(y);

        foreach (var item in items)
        {
            var height = MeasurePackingItemHeight(item);
            if (y + height > ContentBottom)
            {
                pages++;
                y = 116f + 12f;
                y = SimulateHeader(y);
            }
            y += height;
        }

        y += 16f;
        if (y + 82f > ContentBottom)
        {
            pages++;
            y = 116f;
        }

        y += 12f;
        y = SimulateHeader(y);
        foreach (var _ in packages)
        {
            const float height = 24f;
            if (y + height > ContentBottom)
            {
                pages++;
                y = 116f + 12f;
                y = SimulateHeader(y);
            }
            y += height;
        }

        y += 18f;
        if (y + 28f > ContentBottom)
            pages++;

        return pages;
    }

    private static float SimulateHeader(float y) => y + 34f;

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
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            var current = words[0];
            for (var index = 1; index < words.Length; index++)
            {
                var candidate = current + " " + words[index];
                if (paint.MeasureText(candidate) <= width)
                {
                    current = candidate;
                }
                else
                {
                    result.Add(current);
                    current = words[index];
                }
            }
            result.Add(current);
        }
        return result;
    }

    private static IEnumerable<string> SplitLines(string? value)
        => (value ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Trim(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..Math.Max(1, max - 1)] + "…";
    }

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

    private static void RightText(SKCanvas canvas, string? text, float right, float y, SKPaint paint)
        => DocumentPdfStyle.RightText(canvas, text, right, y, paint);

    private sealed record InvoicePagePlan(int StartItemNumber, List<InvoiceItem> Items, bool IsLast);
}
