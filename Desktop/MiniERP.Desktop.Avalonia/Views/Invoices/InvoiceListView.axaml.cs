using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Invoices;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Invoices;

public partial class InvoiceListView : UserControl
{
    private readonly InvoiceType _type;
    private readonly SelectLineSortState _sortState = new();
    private InvoiceListViewModel ViewModel => (InvoiceListViewModel)DataContext!;

    public event Action<Invoice?>? OpenInvoiceRequested;

    public InvoiceListView(InvoiceType type)
    {
        _type = type;
        InitializeComponent();
        DataContext = new InvoiceListViewModel(type);
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

    public async Task ReloadAndSelectAsync(int? invoiceId)
    {
        await ReloadAsync();
        if (invoiceId is null || invoiceId <= 0) return;
        var selected = ViewModel.Invoices.FirstOrDefault(row => row.Id == invoiceId.Value);
        if (selected is null) return;
        ViewModel.SelectedInvoice = selected;
        Dispatcher.UIThread.Post(() =>
        {
            if (InvoiceGrid.Columns.Count > 0)
                InvoiceGrid.ScrollIntoView(selected, InvoiceGrid.Columns[0]);
        }, DispatcherPriority.Loaded);
    }

    public InvoiceType Type => _type;

    private void New_Click(object? sender, RoutedEventArgs e) => OpenInvoiceRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedInvoice is null) { await ViewModel.DeleteSelectedAsync(); return; }
        var confirmed = await ConfirmationDialog.ShowAsync(this, $"Delete {ViewModel.DocumentTitle}", $"Delete '{ViewModel.SelectedInvoice.InvoiceNumber}'? This cannot be undone.");
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
            SelectLineGridSupport.SortInPlace(ViewModel.Invoices, field, _sortState.Ascending);
    }

    private void UpdateSortIndicators()
    {
        NumberSortArrow.Text = _sortState.Arrow("InvoiceNumber");
        CustomerSortArrow.Text = _sortState.Arrow("CustomerNameSnapshot");
        UserSortArrow.Text = _sortState.Arrow("SalesContactNameSnapshot");
        DateSortArrow.Text = _sortState.Arrow("InvoiceDate");
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
        => SelectLineGridSupport.SyncColumnWidths(InvoiceTableLayout, InvoiceGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void InvoiceGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedInvoice is not null) OpenInvoiceRequested?.Invoke(ViewModel.SelectedInvoice);
    }
}
