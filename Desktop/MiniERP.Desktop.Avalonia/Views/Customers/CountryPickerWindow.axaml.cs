using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop.Views.Customers;

public sealed record CountryOption(string Code, string Name);

public partial class CountryPickerWindow : Window
{
    private static readonly CountryOption[] AllCountries =
    {
        new("CN", "China"),
        new("GB", "United Kingdom"),
        new("DE", "Germany"),
        new("FR", "France"),
        new("US", "United States"),
        new("JP", "Japan"),
        new("KR", "Korea, Republic of")
    };

    private readonly ObservableCollection<CountryOption> _countries = new();
    private readonly SelectLineSortState _sortState = new();

    public CountryPickerWindow(string? currentCode = null)
    {
        InitializeComponent();
        CountryGrid.ItemsSource = _countries;
        ApplyFilter();
        Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Loaded);

        if (!string.IsNullOrWhiteSpace(currentCode))
        {
            var current = AllCountries.FirstOrDefault(country => country.Code.Equals(currentCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (current is not null) CountryGrid.SelectedItem = current;
        }
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var codeFilter = CodeFilterTextBox.Text?.Trim() ?? string.Empty;
        var nameFilter = NameFilterTextBox.Text?.Trim() ?? string.Empty;

        _countries.Clear();
        foreach (var country in AllCountries.Where(country =>
                     (string.IsNullOrEmpty(codeFilter) || country.Code.Contains(codeFilter, StringComparison.OrdinalIgnoreCase)) &&
                     (string.IsNullOrEmpty(nameFilter) || country.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))))
        {
            _countries.Add(country);
        }
        ApplySort();
    }

    private void SortHeader_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string field) return;
        _sortState.Toggle(field);
        ApplySort();
        CodeSortArrow.Text = _sortState.Arrow("Code");
        NameSortArrow.Text = _sortState.Arrow("Name");
    }

    private void ApplySort()
    {
        if (_sortState.Field is { } field)
            SelectLineGridSupport.SortInPlace(_countries, field, _sortState.Ascending);
    }

    private void TableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void ColumnSplitter_DragDelta(object? sender, VectorEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void SyncDataGridColumnWidths()
        => SelectLineGridSupport.SyncColumnWidths(CountryTableLayout, CountryGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void CountryGrid_DoubleTapped(object? sender, TappedEventArgs e) => CloseSelected();
    private void Ok_Click(object? sender, RoutedEventArgs e) => CloseSelected();
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
    private void CloseSelected()
    {
        if (CountryGrid.SelectedItem is CountryOption selected) Close(selected.Code);
    }
}
