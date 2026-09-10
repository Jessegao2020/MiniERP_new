using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.PackingLists;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.PackingLists;

public partial class PackingListListView : UserControl
{
    private readonly SelectLineSortState _sortState = new();
    private PackingListListViewModel ViewModel => (PackingListListViewModel)DataContext!;
    public event Action<PackingList?>? OpenPackingListRequested;

    public PackingListListView()
    {
        InitializeComponent();
        DataContext = new PackingListListViewModel();
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

    private void New_Click(object? sender, RoutedEventArgs e) => OpenPackingListRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPackingList is null) { await ViewModel.DeleteSelectedAsync(); return; }
        var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Packing List", $"Delete '{ViewModel.SelectedPackingList.PackingListNumber}'? This cannot be undone.");
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
            SelectLineGridSupport.SortInPlace(ViewModel.PackingLists, field, _sortState.Ascending);
    }

    private void UpdateSortIndicators()
    {
        NumberSortArrow.Text = _sortState.Arrow("PackingListNumber");
        CustomerSortArrow.Text = _sortState.Arrow("CustomerNameSnapshot");
        UserSortArrow.Text = _sortState.Arrow("SalesContactNameSnapshot");
        DateSortArrow.Text = _sortState.Arrow("PackingDate");
        PoSortArrow.Text = _sortState.Arrow("CustomerPoNumber");
        CartonsSortArrow.Text = _sortState.Arrow("TotalCartons");
        GrossSortArrow.Text = _sortState.Arrow("TotalGrossWeight");
        CbmSortArrow.Text = _sortState.Arrow("TotalCbm");
        SourceSortArrow.Text = _sortState.Arrow("SourceDocumentNumber");
    }

    private void TableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void ColumnSplitter_DragDelta(object? sender, VectorEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void SyncDataGridColumnWidths()
        => SelectLineGridSupport.SyncColumnWidths(PackingListTableLayout, PackingListGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void PackingListGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedPackingList is not null) OpenPackingListRequested?.Invoke(ViewModel.SelectedPackingList);
    }
}
