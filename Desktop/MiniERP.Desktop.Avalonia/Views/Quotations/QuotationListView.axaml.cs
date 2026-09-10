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
            Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Loaded);
        };
    }

    public async Task ReloadAsync()
    {
        await ViewModel.LoadAsync();
        ApplySort();
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
            SelectLineGridSupport.SortInPlace(ViewModel.Quotations, field, _sortState.Ascending);
    }

    private void UpdateSortIndicators()
    {
        NumberSortArrow.Text = _sortState.Arrow("QuotationNumber");
        CustomerSortArrow.Text = _sortState.Arrow("Customer.Name");
        UserSortArrow.Text = _sortState.Arrow("User.Name");
        DateSortArrow.Text = _sortState.Arrow("QuotationDate");
        CurrencySortArrow.Text = _sortState.Arrow("Currency");
        TotalSortArrow.Text = _sortState.Arrow("TotalAmount");
        ValidSortArrow.Text = _sortState.Arrow("ValidUntil");
        DeliverySortArrow.Text = _sortState.Arrow("DeliveryTerm");
        LeadSortArrow.Text = _sortState.Arrow("LeadTime");
        PaymentSortArrow.Text = _sortState.Arrow("PaymentTerm");
    }

    private void TableLayout_SizeChanged(object? sender, SizeChangedEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void ColumnSplitter_DragDelta(object? sender, VectorEventArgs e)
        => Dispatcher.UIThread.Post(SyncDataGridColumnWidths, DispatcherPriority.Render);

    private void SyncDataGridColumnWidths()
        => SelectLineGridSupport.SyncColumnWidths(QuotationTableLayout, QuotationGrid);

    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void QuotationGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedQuotation is not null) OpenQuotationRequested?.Invoke(ViewModel.SelectedQuotation);
    }
}
