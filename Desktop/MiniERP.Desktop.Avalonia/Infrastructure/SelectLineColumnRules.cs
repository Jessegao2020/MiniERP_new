using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// Draws the body column separators at the already-arranged header separator
/// positions. This is paint-only: it never changes a column width, measure,
/// arrange, scroll offset or any other layout property of the DataGrid.
/// </summary>
public sealed class SelectLineColumnRules : Control
{
    public static readonly StyledProperty<DataGrid?> TargetProperty =
        AvaloniaProperty.Register<SelectLineColumnRules, DataGrid?>(nameof(Target));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<SelectLineColumnRules, IBrush?>(nameof(LineBrush));

    private Rect[] _rules = Array.Empty<Rect>();

    static SelectLineColumnRules()
    {
        AffectsRender<SelectLineColumnRules>(LineBrushProperty);
    }

    public DataGrid? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        _rules = Array.Empty<Rect>();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var next = ReadRules();
        if (_rules.SequenceEqual(next))
            return;

        _rules = next;
        // A changed drawing does not invalidate layout or schedule width writes.
        InvalidateVisual();
    }

    private Rect[] ReadRules()
    {
        if (Target is not { } grid || Bounds.Width <= 0 || Bounds.Height <= 0)
            return Array.Empty<Rect>();

        var headers = grid.GetVisualDescendants()
            .OfType<DataGridColumnHeadersPresenter>().FirstOrDefault();
        var rows = grid.GetVisualDescendants()
            .OfType<DataGridRowsPresenter>().FirstOrDefault();
        if (headers is null || rows is null ||
            headers.TranslatePoint(default, this) is not { } headerOrigin ||
            rows.TranslatePoint(default, this) is not { } rowOrigin)
            return Array.Empty<Rect>();

        // Match the header's rendered left edges, not the previous cell's
        // rounded right edge. Only paint over realized, visible data rows.
        var left = Math.Max(0, Math.Max(headerOrigin.X, rowOrigin.X));
        var right = Math.Min(Bounds.Width,
            Math.Min(headerOrigin.X + headers.Bounds.Width, rowOrigin.X + rows.Bounds.Width));
        var top = Math.Max(0, rowOrigin.Y);
        var bottom = Math.Min(Bounds.Height, rowOrigin.Y + rows.Bounds.Height);

        // The Fluent template uses overlay scrollbars. Keep them unobscured.
        foreach (var scrollbar in grid.GetVisualDescendants().OfType<ScrollBar>())
        {
            if (!scrollbar.IsVisible ||
                scrollbar.TranslatePoint(default, this) is not { } position)
                continue;

            if (scrollbar.Name == "PART_VerticalScrollbar" && scrollbar.Bounds.Width > 0)
                right = Math.Min(right, position.X);
            else if (scrollbar.Name == "PART_HorizontalScrollbar" && scrollbar.Bounds.Height > 0)
                bottom = Math.Min(bottom, position.Y);
        }

        var lastRowBottom = top;
        foreach (var row in rows.Children.OfType<DataGridRow>())
        {
            if (!row.IsVisible || row.Bounds.Height <= 0 ||
                row.TranslatePoint(default, this) is not { } position)
                continue;
            lastRowBottom = Math.Max(lastRowBottom, position.Y + row.Bounds.Height);
        }
        bottom = Math.Min(bottom, lastRowBottom);
        if (bottom <= top || right <= left)
            return Array.Empty<Rect>();

        var rules = new List<Rect>();
        foreach (var separator in headers.GetVisualDescendants().OfType<Rectangle>())
        {
            if (!separator.Classes.Contains("selectline-column-separator") ||
                !separator.IsVisible || separator.Bounds.Width <= 0 ||
                separator.TranslatePoint(default, this) is not { } position)
                continue;

            var x = Math.Max(left, position.X);
            var end = Math.Min(right, position.X + separator.Bounds.Width);
            if (end > x)
                rules.Add(new Rect(x, top, end - x, bottom - top));
        }

        return rules.ToArray();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (LineBrush is not { } brush)
            return;

        foreach (var rule in _rules)
            context.DrawRectangle(brush, null, rule);
    }
}
