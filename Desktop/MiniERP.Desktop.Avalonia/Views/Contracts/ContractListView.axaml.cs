using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Contracts;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Contracts;

public partial class ContractListView : UserControl
{
    private readonly SelectLineSortState _sortState = new();
    private ContractListViewModel ViewModel => (ContractListViewModel)DataContext!;

    public event Action<Contract?>? OpenContractRequested;

    public ContractListView()
    {
        InitializeComponent();
        DataContext = new ContractListViewModel();
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

    public async Task ReloadAndSelectAsync(int? contractId)
    {
        await ReloadAsync();
        if (contractId is null || contractId <= 0) return;
        var selected = ViewModel.Contracts.FirstOrDefault(row => row.Id == contractId.Value);
        if (selected is null) return;
        ViewModel.SelectedContract = selected;
        Dispatcher.UIThread.Post(() =>
        {
            if (ContractGrid.Columns.Count > 0)
                ContractGrid.ScrollIntoView(selected, ContractGrid.Columns[0]);
        }, DispatcherPriority.Loaded);
    }

    private void New_Click(object? sender, RoutedEventArgs e) => OpenContractRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedContract is null) { await ViewModel.DeleteSelectedAsync(); return; }
        var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Contract", $"Delete '{ViewModel.SelectedContract.ContractNumber}'? This cannot be undone.");
        if (confirmed) await ViewModel.DeleteSelectedAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        ApplySort();
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is string field)
        {
            ViewModel.SetFilter(field, textBox.Text);
            ApplySort();
        }
    }

    private void Grid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        if (SelectLineGridSupport.GetSortField(e.Column) is not string field) return;
        _sortState.Toggle(field);
        ApplySort();
        UpdateSortIndicators();
    }

    private void ApplySort()
    {
        if (_sortState.Field is { } field)
            SelectLineGridSupport.SortInPlace(ViewModel.Contracts, field, _sortState.Ascending);
    }

    private void UpdateSortIndicators()
        => SelectLineGridSupport.UpdateSortIndicators(ContractGrid, _sortState.Arrow);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void ContractGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!SelectLineGridSupport.IsDataRowEvent(e)) return;
        if (ViewModel.SelectedContract is not null) OpenContractRequested?.Invoke(ViewModel.SelectedContract);
    }
}
