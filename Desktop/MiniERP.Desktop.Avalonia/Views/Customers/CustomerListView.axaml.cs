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

    private void Grid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        if (SelectLineGridSupport.GetSortField(e.Column) is not string field)
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
        => SelectLineGridSupport.UpdateSortIndicators(CustomerGrid, _sortState.Arrow);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void CustomerGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!SelectLineGridSupport.IsDataRowEvent(e)) return;
        if (ViewModel.SelectedCustomer is not null)
            OpenCustomerRequested?.Invoke(ViewModel.SelectedCustomer);
    }
}
