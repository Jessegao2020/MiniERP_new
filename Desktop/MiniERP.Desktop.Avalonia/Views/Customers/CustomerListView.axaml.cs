using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Customers;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

public partial class CustomerListView : UserControl
{
    private readonly SelectLineSortState _sortState = new();
    private CustomerListViewModel ViewModel => (CustomerListViewModel)DataContext!;

    public event Action<Customer?>? OpenCustomerRequested;

    public CustomerListView()
    {
        InitializeComponent();
        DataContext = new CustomerListViewModel();
        AttachedToVisualTree += async (_, _) =>
        {
            await ViewModel.LoadAsync();
            ApplySort();
            Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Loaded);
        };
    }

    public async Task ReloadAsync()
    {
        await ViewModel.LoadAsync();
        ApplySort();
    }

    public async Task ReloadAndSelectAsync(int? customerId)
    {
        await ReloadAsync();
        if (customerId is null || customerId <= 0) return;

        var selected = ViewModel.Customers.FirstOrDefault(row => row.Id == customerId.Value);
        if (selected is null) return;

        ViewModel.SelectedCustomer = selected;
        Dispatcher.UIThread.Post(() =>
        {
            if (CustomerGrid.Columns.Count > 0)
                CustomerGrid.ScrollIntoView(selected, CustomerGrid.Columns[0]);
        }, DispatcherPriority.Loaded);
    }

    private void New_Click(object? sender, RoutedEventArgs e)
        => OpenCustomerRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCustomer is null)
        {
            await ViewModel.DeleteSelectedAsync();
            return;
        }

        var confirmed = await ConfirmationDialog.ShowAsync(
            this,
            "Delete Customer",
            $"Delete customer '{ViewModel.SelectedCustomer.Name}'? This cannot be undone.");

        if (confirmed)
            await ViewModel.DeleteSelectedAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        ApplySort();
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field)
            return;

        ViewModel.SetFilter(field, textBox.Text);
        ApplySort();
    }

    private void SortHeader_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string field)
            return;

        _sortState.Toggle(field);
        ApplySort();
        UpdateSortIndicators();
    }

    private void ApplySort()
    {
        if (_sortState.Field is { } field)
            SelectLineGridSupport.SortInPlace(ViewModel.Customers, field, _sortState.Ascending);
    }

    private void UpdateSortIndicators()
    {
        NameSortArrow.Text = _sortState.Arrow("Name");
        AddressSortArrow.Text = _sortState.Arrow("AddressLine1");
        CitySortArrow.Text = _sortState.Arrow("City");
        StateSortArrow.Text = _sortState.Arrow("State");
        PostalSortArrow.Text = _sortState.Arrow("PostalCode");
        CountrySortArrow.Text = _sortState.Arrow("Country");
    }

    private void TableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void ColumnSplitter_DragDelta(object? sender, VectorEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void SyncDataGridColumnWidths()
        => SelectLineGridSupport.SyncColumnWidths(CustomerTableLayout, CustomerGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void CustomerGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedCustomer is not null)
            OpenCustomerRequested?.Invoke(ViewModel.SelectedCustomer);
    }
}
