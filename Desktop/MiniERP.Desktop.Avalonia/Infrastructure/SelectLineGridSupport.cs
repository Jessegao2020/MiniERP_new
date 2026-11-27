using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Controls;

namespace MiniERP.Desktop.Infrastructure;

public sealed class SelectLineSortState
{
    public string? Field { get; private set; }
    public bool Ascending { get; private set; } = true;

    public void Toggle(string field)
    {
        if (string.Equals(Field, field, StringComparison.OrdinalIgnoreCase))
            Ascending = !Ascending;
        else
        {
            Field = field;
            Ascending = true;
        }
    }

    public string Arrow(string field)
        => string.Equals(Field, field, StringComparison.OrdinalIgnoreCase)
            ? (Ascending ? "▲" : "▼")
            : string.Empty;
}

public static class SelectLineGridSupport
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Grid, DataGrid> SynchronizedLayouts = new();

    public static void SyncColumnWidths(Grid layout, DataGrid grid)
    {
        if (!SynchronizedLayouts.TryGetValue(layout, out _))
        {
            SynchronizedLayouts.Add(layout, grid);

            // A splitter can change the column boundaries without changing the
            // outer Grid's size. Read the final header bounds after arrange.
            layout.LayoutUpdated += (_, _) => ApplyColumnWidths(layout, grid);
        }

        ApplyColumnWidths(layout, grid);
    }

    private static void ApplyColumnWidths(Grid layout, DataGrid grid)
    {
        var count = layout.ColumnDefinitions.Count;
        if (count == 0 || grid.Columns.Count != count ||
            !layout.IsArrangeValid || !grid.IsArrangeValid)
            return;

        var headers = layout.Children.OfType<Border>()
            .Where(border => Grid.GetRow(border) == 0 &&
                             border.Classes.Contains("grid-header"))
            .OrderBy(border => Grid.GetColumn(border))
            .ToArray();

        if (headers.Length != count || headers.Any(header => header.Bounds.Width <= 0))
            return;

        var scale = TopLevel.GetTopLevel(layout)?.RenderScaling ?? 1d;
        double Snap(double value) => layout.UseLayoutRounding
            ? Math.Round(value * scale) / scale
            : value;
        double BorderInset(double value) => grid.UseLayoutRounding
            ? Math.Round(value * scale) / scale
            : value;

        // The header and filter share the same Grid. Use their arranged
        // boundaries instead of independent star widths or rounded widths.
        // The DataGrid's outer border consumes space inside its first and
        // last columns, so account for it without changing its appearance.
        var left = grid.Bounds.X + BorderInset(grid.BorderThickness.Left);
        var widths = new double[count];

        for (var i = 0; i < count; i++)
        {
            if (Grid.GetColumn(headers[i]) != i)
                return;

            var right = Snap(headers[i].Bounds.Right);
            if (i == count - 1)
                right -= BorderInset(grid.BorderThickness.Right);

            widths[i] = right - left;
            if (!double.IsFinite(widths[i]) || widths[i] <= 0)
                return;

            left = right;
        }

        // Do not invalidate layout on every LayoutUpdated notification.
        // Once the columns are fixed, only a real boundary change needs a write.
        for (var i = 0; i < count; i++)
        {
            var width = grid.Columns[i].Width;
            if (!width.IsAbsolute || Math.Abs(width.Value - widths[i]) > 0.0001)
                grid.Columns[i].Width = new DataGridLength(widths[i]);
        }
    }

    public static void ApplyAlternateRow(DataGridRowEventArgs e)
    {
        const string alternateClass = "alternate";
        if (e.Row.Index % 2 == 1)
        {
            if (!e.Row.Classes.Contains(alternateClass))
                e.Row.Classes.Add(alternateClass);
        }
        else
        {
            e.Row.Classes.Remove(alternateClass);
        }
    }

    public static void SortInPlace<T>(ObservableCollection<T> items, string propertyPath, bool ascending)
    {
        var comparer = PropertyValueComparer.Instance;
        var indexed = items.Select((item, index) => new IndexedValue<T>(item, index));
        var sorted = ascending
            ? indexed.OrderBy(entry => GetPropertyValue(entry.Item, propertyPath), comparer).ThenBy(entry => entry.Index)
            : indexed.OrderByDescending(entry => GetPropertyValue(entry.Item, propertyPath), comparer).ThenBy(entry => entry.Index);

        var ordered = sorted.Select(entry => entry.Item).ToList();
        items.Clear();
        foreach (var item in ordered)
            items.Add(item);
    }

    private static object? GetPropertyValue(object? instance, string path)
    {
        if (instance is null)
            return null;

        object? current = instance;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is null)
                return null;

            var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null)
                return null;

            current = property.GetValue(current);
        }

        return current;
    }

    private readonly record struct IndexedValue<T>(T Item, int Index);

    private sealed class PropertyValueComparer : IComparer<object?>
    {
        public static PropertyValueComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            if (x is string xs && y is string ys)
                return StringComparer.OrdinalIgnoreCase.Compare(xs, ys);

            if (x is IComparable comparable)
            {
                try { return comparable.CompareTo(y); }
                catch { }
            }

            return StringComparer.OrdinalIgnoreCase.Compare(x.ToString(), y.ToString());
        }
    }
}
