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
        if (grid.Columns.Count != layout.ColumnDefinitions.Count)
            return;

        for (var i = 0; i < grid.Columns.Count; i++)
        {
            var width = layout.ColumnDefinitions[i].ActualWidth;
            if (width > 0)
                grid.Columns[i].Width = new DataGridLength(width);
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
