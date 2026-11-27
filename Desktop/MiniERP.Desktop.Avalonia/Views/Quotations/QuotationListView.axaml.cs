using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationListView : UserControl
{
    private readonly SelectLineSortState _sortState = new();
    private QuotationListViewModel ViewModel => (QuotationListViewModel)DataContext!;

    public event Action<Quotation?>? OpenQuotationRequested;

    public QuotationListView()
    {
        InitializeComponent();
        DataContext = new QuotationListViewModel();
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

    public async Task ReloadAndSelectAsync(int? quotationId)
    {
        await ReloadAsync();
        if (quotationId is null || quotationId <= 0) return;
        var selected = ViewModel.Quotations.FirstOrDefault(row => row.Id == quotationId.Value);
        if (selected is null) return;
        ViewModel.SelectedQuotation = selected;
        Dispatcher.UIThread.Post(() =>
        {
            if (QuotationGrid.Columns.Count > 0)
                QuotationGrid.ScrollIntoView(selected, QuotationGrid.Columns[0]);
        }, DispatcherPriority.Loaded);
    }

    private void New_Click(object? sender, RoutedEventArgs e) => OpenQuotationRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuotation is null) { await ViewModel.DeleteSelectedAsync(); return; }
        var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Quotation", $"Delete quotation '{ViewModel.SelectedQuotation.QuotationNumber}'? This cannot be undone.");
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
            SelectLineGridSupport.SortInPlace(ViewModel.Quotations, field, _sortState.Ascending);
    }

    private void UpdateSortIndicators()
        => SelectLineGridSupport.UpdateSortIndicators(QuotationGrid, _sortState.Arrow);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void QuotationGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!SelectLineGridSupport.IsDataRowEvent(e)) return;
        if (ViewModel.SelectedQuotation is not null) OpenQuotationRequested?.Invoke(ViewModel.SelectedQuotation);
    }
}
