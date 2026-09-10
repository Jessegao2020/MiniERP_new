using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class CustomerPickerWindow : Window
{
    private readonly List<Customer> _allCustomers;
    private readonly ObservableCollection<Customer> _visibleCustomers = new();
    private readonly Dictionary<string, string> _filters = new(StringComparer.OrdinalIgnoreCase);
    private readonly SelectLineSortState _sortState = new();
    private readonly int? _currentCustomerId;

    public CustomerPickerWindow(IEnumerable<Customer> customers, int? currentCustomerId = null)
    {
        InitializeComponent();
        _allCustomers = customers.Where(customer => customer.IsActive).OrderBy(customer => customer.Name).ToList();
        _currentCustomerId = currentCustomerId;
        CustomerGrid.ItemsSource = _visibleCustomers;
        ApplyFilters();
        Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Loaded);
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field) return;
        var value = textBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)) _filters.Remove(field); else _filters[field] = value;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<Customer> filtered = _allCustomers;
        foreach (var filter in _filters)
        {
            filtered = filtered.Where(customer => filter.Key switch
            {
                "Name" => Matches(customer.Name, filter.Value),
                "AddressLine1" => Matches(customer.AddressLine1, filter.Value),
                "PostalCode" => Matches(customer.PostalCode, filter.Value),
                "City" => Matches(customer.City, filter.Value),
                "State" => Matches(customer.State, filter.Value),
                "Country" => Matches(customer.Country, filter.Value),
                _ => true
            });
        }

        _visibleCustomers.Clear();
        foreach (var customer in filtered) _visibleCustomers.Add(customer);
        ApplySort();

        if (_currentCustomerId is not null && CustomerGrid.SelectedItem is null)
        {
            var current = _visibleCustomers.FirstOrDefault(customer => customer.Id == _currentCustomerId.Value);
            if (current is not null) CustomerGrid.SelectedItem = current;
        }

        StatusText.Text = _filters.Count == 0 ? $"{_visibleCustomers.Count} active customer(s)" : $"{_visibleCustomers.Count} of {_allCustomers.Count} active customer(s)";
    }

    private void SortHeader_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string field) return;
        _sortState.Toggle(field);
        ApplySort();
        NameSortArrow.Text = _sortState.Arrow("Name");
        AddressSortArrow.Text = _sortState.Arrow("AddressLine1");
        PostalSortArrow.Text = _sortState.Arrow("PostalCode");
        CitySortArrow.Text = _sortState.Arrow("City");
        StateSortArrow.Text = _sortState.Arrow("State");
        CountrySortArrow.Text = _sortState.Arrow("Country");
    }

    private void ApplySort()
    {
        if (_sortState.Field is { } field)
            SelectLineGridSupport.SortInPlace(_visibleCustomers, field, _sortState.Ascending);
    }

    private void TableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void ColumnSplitter_DragDelta(object? sender, VectorEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void SyncDataGridColumnWidths()
        => SelectLineGridSupport.SyncColumnWidths(CustomerTableLayout, CustomerGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private static bool Matches(string? value, string filter)
        => (value ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void CustomerGrid_DoubleTapped(object? sender, TappedEventArgs e) => CloseSelected();
    private void Ok_Click(object? sender, RoutedEventArgs e) => CloseSelected();
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
    private void CloseSelected()
    {
        if (CustomerGrid.SelectedItem is Customer customer) Close(customer);
    }
}
