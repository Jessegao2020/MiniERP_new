using System.Text;
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

    // Keep the normal Latin family for Western-language text (including Turkish),
    // and use the CJK typeface only for glyphs the Latin family cannot render.
    private static readonly SKTypeface LatinRegular = FindLatinTypeface(SKFontStyle.Normal);
    private static readonly SKTypeface LatinBold = FindLatinTypeface(SKFontStyle.Bold);
    private static readonly SKTypeface LatinItalic = FindLatinTypeface(SKFontStyle.Italic);

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
        DrawText(canvas, "Baoding Forlinx Embedded Technology Co., Ltd", Left + 2f, y, company);
        DrawText(canvas, "2699 Xiangyang North Street", Left + 2f, y + lineStep, text);
        DrawText(canvas, "071000 Baoding", Left + 2f, y + lineStep * 2f, text);
        DrawText(canvas, "China", Left + 2f, y + lineStep * 3f, text);

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

    /// <summary>
    /// Draw text with per-glyph fallback. Skia's PDF canvas does not automatically
    /// fall back to another font when the selected typeface lacks a glyph.
    /// </summary>
    public static void DrawText(SKCanvas canvas, string? text, float x, float baseline, SKPaint paint)
    {
        var value = text ?? string.Empty;
        if (value.Length == 0)
            return;

        var currentX = x;
        foreach (var run in BuildFontRuns(value, paint))
        {
            using var runPaint = CopyPaint(paint, run.Typeface);
            canvas.DrawText(run.Text, currentX, baseline, runPaint);
            currentX += runPaint.MeasureText(run.Text);
        }
    }

    public static float MeasureText(string? text, SKPaint paint)
    {
        var value = text ?? string.Empty;
        var width = 0f;
        foreach (var run in BuildFontRuns(value, paint))
        {
            using var runPaint = CopyPaint(paint, run.Typeface);
            width += runPaint.MeasureText(run.Text);
        }
        return width;
    }

    public static void RightText(SKCanvas canvas, string? text, float rightX, float baseline, SKPaint paint)
    {
        var value = text ?? string.Empty;
        DrawText(canvas, value, rightX - MeasureText(value, paint), baseline, paint);
    }

    public static void CenterText(SKCanvas canvas, string? text, float centerX, float baseline, SKPaint paint)
    {
        var value = text ?? string.Empty;
        DrawText(canvas, value, centerX - MeasureText(value, paint) / 2f, baseline, paint);
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
        DrawText(canvas, label, labelX, y, labelPaint);
        DrawText(canvas, value, valueX, y, valuePaint);
    }

    private static SKTypeface FindTypeface(SKFontStyle style)
    {
        // Pick a CJK-capable primary font. Western-language runs are deliberately
        // drawn with the Latin companion font so Turkish glyphs keep working.
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
            if (typeface is not null && SupportsCjkPdf(typeface))
                return typeface;

            typeface?.Dispose();
        }

        var fallback = SKFontManager.Default.MatchCharacter('中');
        if (fallback is not null)
        {
            var styledFallback = SKTypeface.FromFamilyName(fallback.FamilyName, style);
            if (styledFallback is not null && SupportsCjkPdf(styledFallback))
            {
                fallback.Dispose();
                return styledFallback;
            }

            styledFallback?.Dispose();
            if (SupportsCjkPdf(fallback))
                return fallback;

            fallback.Dispose();
        }

        return FindLatinTypeface(style);
    }

    private static SKTypeface FindLatinTypeface(SKFontStyle style)
    {
        foreach (var family in new[]
                 {
                     "Arial",
                     "Liberation Sans",
                     "DejaVu Sans",
                     "Noto Sans",
                     "FreeSans"
                 })
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null && SupportsLatinExtended(typeface))
                return typeface;

            typeface?.Dispose();
        }

        return SKTypeface.Default;
    }

    private static IEnumerable<FontRun> BuildFontRuns(string text, SKPaint paint)
    {
        var primary = paint.Typeface ?? Regular;
        var latin = LatinTypefaceFor(paint);
        var builder = new StringBuilder();
        SKTypeface? currentTypeface = null;

        foreach (var ch in text)
        {
            // Prefer the Latin family whenever possible so Western-language text
            // remains visually consistent. Fall back to CJK for Chinese and symbols
            // not present in the Latin font.
            var typeface = latin.ContainsGlyphs(ch.ToString())
                ? latin
                : primary.ContainsGlyphs(ch.ToString())
                    ? primary
                    : primary;

            if (currentTypeface is not null && !ReferenceEquals(typeface, currentTypeface))
            {
                yield return new FontRun(builder.ToString(), currentTypeface);
                builder.Clear();
            }

            currentTypeface = typeface;
            builder.Append(ch);
        }

        if (builder.Length > 0 && currentTypeface is not null)
            yield return new FontRun(builder.ToString(), currentTypeface);
    }

    private static SKTypeface LatinTypefaceFor(SKPaint paint)
    {
        var style = paint.Typeface?.FontStyle ?? SKFontStyle.Normal;
        if (style.Slant != SKFontStyleSlant.Upright)
            return LatinItalic;
        if (style.Weight >= 600)
            return LatinBold;
        return LatinRegular;
    }

    private static SKPaint CopyPaint(SKPaint source, SKTypeface typeface)
        => new()
        {
            IsAntialias = source.IsAntialias,
            TextSize = source.TextSize,
            Typeface = typeface,
            Color = source.Color,
            Style = source.Style
        };

    private static bool SupportsCjkPdf(SKTypeface typeface)
        => typeface.ContainsGlyphs("A中℃");

    private static bool SupportsLatinExtended(SKTypeface typeface)
        => typeface.ContainsGlyphs("AÇçĞğİıÖöŞşÜü");

    private sealed record FontRun(string Text, SKTypeface Typeface);
}
