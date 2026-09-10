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
            Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Loaded);
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

    private void SortHeader_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string field) return;
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
    {
        NumberSortArrow.Text = _sortState.Arrow("ContractNumber");
        CustomerSortArrow.Text = _sortState.Arrow("CustomerNameSnapshot");
        UserSortArrow.Text = _sortState.Arrow("SalesContactNameSnapshot");
        DateSortArrow.Text = _sortState.Arrow("ContractDate");
        PoSortArrow.Text = _sortState.Arrow("CustomerPoNumber");
        CurrencySortArrow.Text = _sortState.Arrow("Currency");
        TotalSortArrow.Text = _sortState.Arrow("TotalAmount");
        SourceSortArrow.Text = _sortState.Arrow("SourceDocumentNumber");
    }

    private void TableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void ColumnSplitter_DragDelta(object? sender, VectorEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void SyncDataGridColumnWidths()
        => SelectLineGridSupport.SyncColumnWidths(ContractTableLayout, ContractGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void ContractGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedContract is not null) OpenContractRequested?.Invoke(ViewModel.SelectedContract);
    }
}
