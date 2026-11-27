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
    public static void SyncColumnWidths(Grid layout, DataGrid grid)
    {
        // Windows can hang while applying custom DataGrid column widths at
        // certain maximized window sizes (confirmed by the diagnostic bypass).
        // Keep Avalonia's native column sizing by default on Windows until the
        // synchronization can be replaced and verified on a real Windows host.
        // The explicit opt-in is for developer diagnostics only.
        if (Environment.GetEnvironmentVariable("MINIERP_DISABLE_COLUMN_WIDTH_SYNC") == "1"
            || (OperatingSystem.IsWindows()
                && Environment.GetEnvironmentVariable("MINIERP_ENABLE_COLUMN_WIDTH_SYNC") != "1"))
            return;

        if (grid.Columns.Count != layout.ColumnDefinitions.Count)
            return;

        for (var i = 0; i < grid.Columns.Count; i++)
        {
            var width = layout.ColumnDefinitions[i].ActualWidth;
            if (!double.IsFinite(width) || width <= 0)
                continue;

            var column = grid.Columns[i];
            var current = column.Width;

            // Avoid invalidating the DataGrid layout for unchanged widths or
            // insignificant floating-point jitter during DPI-dependent layout.
            // Always convert star/auto widths on the first synchronization.
            if (current.IsAbsolute && Math.Abs(current.Value - width) < 0.1)
                continue;

            column.Width = new DataGridLength(width);
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
