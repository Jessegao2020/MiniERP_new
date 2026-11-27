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
        };
    }

    public async Task ReloadAsync()
    {
        await ViewModel.LoadAsync();
        ApplySort();
    }

    public async Task ReloadAndSelectAsync(int? packingListId)
    {
        await ReloadAsync();
        if (packingListId is null || packingListId <= 0) return;
        var selected = ViewModel.PackingLists.FirstOrDefault(row => row.Id == packingListId.Value);
        if (selected is null) return;
        ViewModel.SelectedPackingList = selected;
        Dispatcher.UIThread.Post(() =>
        {
            if (PackingListGrid.Columns.Count > 0)
                PackingListGrid.ScrollIntoView(selected, PackingListGrid.Columns[0]);
        }, DispatcherPriority.Loaded);
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
            SelectLineGridSupport.SortInPlace(ViewModel.PackingLists, field, _sortState.Ascending);
    }

    private void UpdateSortIndicators()
        => SelectLineGridSupport.UpdateSortIndicators(PackingListGrid, _sortState.Arrow);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void PackingListGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!SelectLineGridSupport.IsDataRowEvent(e)) return;
        if (ViewModel.SelectedPackingList is not null) OpenPackingListRequested?.Invoke(ViewModel.SelectedPackingList);
    }
}
