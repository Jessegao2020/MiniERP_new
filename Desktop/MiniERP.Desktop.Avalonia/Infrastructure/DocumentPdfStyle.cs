using SkiaSharp;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Shared print geometry copied from the approved Quotation PDF template.
/// Other sales documents use this so branding, typography and footer geometry
/// stay visually identical while their business content remains document-specific.
/// </summary>
internal static class DocumentPdfStyle
{
    public const float PageWidth = 595f;
    public const float PageHeight = 842f;
    public const float Left = 58f;
    public const float Right = 536f;
    public const float FooterLineY = 750f;

    public static readonly SKTypeface Regular = FindTypeface(SKFontStyle.Normal);
    public static readonly SKTypeface Bold = FindTypeface(SKFontStyle.Bold);
    public static readonly SKTypeface Italic = FindTypeface(SKFontStyle.Italic);

    public static void DrawBrandHeader(SKCanvas canvas, float logoBaseline, float lineY)
    {
        // This is the final approved Quotation placement. The source PNG contains
        // transparent top padding, so -17f aligns the visible logo top with the
        // company-name text block.
        DrawOfficialLogo(canvas, Left, logoBaseline - 17f);

        using var company = Paint(10.2f, Bold);
        using var tagline = Paint(7.1f, Italic);
        RightText(canvas, "Baoding Forlinx Embedded Technology Co., Ltd", Right - 3f, logoBaseline - 4f, company);
        RightText(canvas, "Trusted Designer & Manufacturer of System on Module", Right - 3f, logoBaseline + 9f, tagline);

        using var rule = Stroke(0.8f, new SKColor(70, 70, 70));
        canvas.DrawLine(Left, lineY, Right, lineY, rule);
    }

    public static void DrawFooter(SKCanvas canvas, int pageNumber, int pageCount)
    {
        using var rule = Stroke(0.75f, new SKColor(70, 70, 70));
        using var company = Paint(6.5f, Bold);
        using var label = Paint(6.5f, Bold);
        using var text = Paint(6.5f, Regular);
        using var page = Paint(8f, Regular, new SKColor(90, 90, 90));

        canvas.DrawLine(Left, FooterLineY, Right, FooterLineY, rule);

        const float y = 770f;
        const float lineStep = 10.5f;
        canvas.DrawText("Baoding Forlinx Embedded Technology Co., Ltd", Left + 2f, y, company);
        canvas.DrawText("2699 Xiangyang North Street", Left + 2f, y + lineStep, text);
        canvas.DrawText("071000 Baoding", Left + 2f, y + lineStep * 2f, text);
        canvas.DrawText("China", Left + 2f, y + lineStep * 3f, text);

        DrawMeta(canvas, "Bank Name:", "China Construction Bank", 286f, 359f, y, label, text);
        DrawMeta(canvas, "Bank Address:", "345 Longxing West Rd, Baoding, China", 286f, 359f, y + lineStep, label, text);
        DrawMeta(canvas, "Bank Account:", "1301 4600 6002 2010 0241", 286f, 359f, y + lineStep * 2f, label, text);
        DrawMeta(canvas, "Swift Code:", "PCBCCNBJ", 286f, 359f, y + lineStep * 3f, label, text);

        CenterText(canvas, $"Page {pageNumber} of {pageCount}", PageWidth / 2f, 826f, page);
    }

    public static void DrawContinuedOnNextPage(SKCanvas canvas, float y)
    {
        using var notice = Paint(7f, Italic, new SKColor(90, 90, 90));
        RightText(canvas, "Continued on next page", Right, y, notice);
    }

    public static SKPaint Paint(float size, SKTypeface typeface, SKColor? color = null)
        => new()
        {
            IsAntialias = true,
            TextSize = size,
            Typeface = typeface,
            Color = color ?? SKColors.Black
        };

    public static SKPaint Stroke(float width, SKColor color)
        => new()
        {
            IsAntialias = true,
            StrokeWidth = width,
            Color = color,
            Style = SKPaintStyle.Stroke
        };

    public static void RightText(SKCanvas canvas, string? text, float rightX, float baseline, SKPaint paint)
    {
        var value = text ?? string.Empty;
        canvas.DrawText(value, rightX - paint.MeasureText(value), baseline, paint);
    }

    public static void CenterText(SKCanvas canvas, string? text, float centerX, float baseline, SKPaint paint)
    {
        var value = text ?? string.Empty;
        canvas.DrawText(value, centerX - paint.MeasureText(value) / 2f, baseline, paint);
    }

    private static void DrawOfficialLogo(SKCanvas canvas, float x, float y)
    {
        using var bitmap = SKBitmap.Decode(DocumentBrandAssets.ForlinxLogoPng)
            ?? throw new InvalidOperationException("The official Forlinx logo asset could not be decoded.");
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        const float width = 132f;
        var height = width * bitmap.Height / bitmap.Width;
        canvas.DrawBitmap(bitmap, SKRect.Create(x, y, width, height), paint);
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
        canvas.DrawText(value, valueX, y, valuePaint);
    }

    private static SKTypeface FindTypeface(SKFontStyle style)
    {
        // A single Skia typeface does not automatically fall back per glyph when
        // text is drawn into a PDF. Arial/Liberation/DejaVu therefore render CJK
        // characters and symbols such as U+2103 (℃) as tofu boxes. Prefer a font
        // family that actually contains the glyphs we need so Skia embeds them in
        // the generated PDF instead of relying on the PDF viewer's local fonts.
        foreach (var family in new[]
                 {
                     "Noto Sans CJK SC",
                     "Noto Sans CJK",
                     "Noto Sans SC",
                     "Source Han Sans SC",
                     "WenQuanYi Micro Hei",
                     "WenQuanYi Zen Hei",
                     "Microsoft YaHei",
                     "SimSun",
                     "Arial Unicode MS"
                 })
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null && SupportsPdfUnicode(typeface))
                return typeface;

            typeface?.Dispose();
        }

        // Let the operating system choose a Chinese-capable fallback if none of
        // the common family names above exists. This works well on Linux through
        // fontconfig and on Windows through the native font manager.
        var fallback = SKFontManager.Default.MatchCharacter('中');
        if (fallback is not null)
        {
            var styledFallback = SKTypeface.FromFamilyName(fallback.FamilyName, style);
            if (styledFallback is not null && SupportsPdfUnicode(styledFallback))
            {
                fallback.Dispose();
                return styledFallback;
            }

            styledFallback?.Dispose();
            if (SupportsPdfUnicode(fallback))
                return fallback;

            fallback.Dispose();
        }

        // Last-resort Latin fallback. This keeps export functional on systems with
        // no CJK font installed, although such a system still cannot render Chinese.
        foreach (var family in new[] { "Arial", "Liberation Sans", "DejaVu Sans" })
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null)
                return typeface;
        }

        return SKTypeface.Default;
    }

    private static bool SupportsPdfUnicode(SKTypeface typeface)
        => typeface.ContainsGlyphs("A中℃");
}
