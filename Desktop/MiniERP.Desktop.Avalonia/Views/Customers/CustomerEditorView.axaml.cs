using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Customers;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

public partial class CustomerEditorView : UserControl
{
    private readonly EditorDirtyMonitor _dirtyMonitor;
    private int _quoteHistoryLoadVersion;
    private bool _openingQuotation;
    private bool _stopped;
    private CustomerEditorViewModel ViewModel => (CustomerEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;
    public event Func<Quotation, Task>? OpenQuotationRequested;

    public int CustomerId => ViewModel.Customer.Id;

    public CustomerEditorView(Customer? customer)
    {
        InitializeComponent();
        DataContext = new CustomerEditorViewModel(customer);
        ShowAddress();

        var saveButton = EditorWorkflowSupport.AddGridViewToggle(this, GridView_Click);
        _dirtyMonitor = new EditorDirtyMonitor(saveButton, CaptureEditState);
        AttachedToVisualTree += async (_, _) =>
        {
            Dispatcher.UIThread.Post(_dirtyMonitor.Start, DispatcherPriority.Loaded);
            if (QuoteHistoryPanel.IsVisible)
                await LoadQuoteHistoryAsync();
        };
    }

    public void StopTracking()
    {
        _stopped = true;
        _quoteHistoryLoadVersion++;
        _dirtyMonitor.Dispose();
    }

    private string CaptureEditState()
        => EditorWorkflowSupport.Snapshot(new
        {
            ViewModel.Customer,
            ViewModel.CountryCode,
            Contacts = ViewModel.Contacts.ToArray()
        });

    private void GridView_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        if (QuoteHistoryPanel.IsVisible)
            await LoadQuoteHistoryAsync();
    }

    private void Discard_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsNew)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Customer", $"Delete customer '{ViewModel.Customer.Name}'? This cannot be undone.");
            if (!confirmed) return;
        }

        if (!await ViewModel.DeleteAsync()) return;
        Deleted?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private async void PickCountry_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var picker = new CountryPickerWindow(ViewModel.Customer.Country);
        var selectedCode = await picker.ShowDialog<string?>(owner);
        if (!string.IsNullOrWhiteSpace(selectedCode)) ViewModel.SetCountry(selectedCode);
    }

    private void Address_Click(object? sender, RoutedEventArgs e) => ShowAddress();
    private void Contact_Click(object? sender, RoutedEventArgs e) => ShowContacts();
    private async void QuoteHistory_Click(object? sender, RoutedEventArgs e)
    {
        AddressPanel.IsVisible = false;
        ContactPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        QuoteHistoryPanel.IsVisible = true;
        await LoadQuoteHistoryAsync();
    }

    private async Task LoadQuoteHistoryAsync()
    {
        if (_stopped) return;
        var version = ++_quoteHistoryLoadVersion;
        var customerId = CustomerId;
        QuoteHistoryGrid.SelectedItem = null;
        QuoteHistoryGrid.ItemsSource = Array.Empty<Quotation>();

        if (customerId <= 0)
        {
            QuoteHistoryStatus.Text = "Save the customer before viewing quotation history.";
            return;
        }

        QuoteHistoryStatus.Text = "Loading quotation history...";
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            var quotations = (await service.GetQuotationsByCustomerIdAsync(customerId))
                .OrderByDescending(q => q.QuotationDate).ThenByDescending(q => q.Id).ToList();

            if (_stopped || version != _quoteHistoryLoadVersion || customerId != CustomerId) return;
            QuoteHistoryGrid.ItemsSource = quotations;
            QuoteHistoryStatus.Text = quotations.Count == 0
                ? "No quotations for this customer."
                : $"{quotations.Count} quotation(s). Double-click a row to open it.";
        }
        catch (Exception ex)
        {
            if (!_stopped && version == _quoteHistoryLoadVersion)
                QuoteHistoryStatus.Text = $"Could not load quotation history: {ex.Message}";
        }
    }

    private async void QuoteHistoryGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Only an actual row, not a column header or empty grid area, can open a document.
        if (_stopped || _openingQuotation || e.Source is not Visual source) return;
        var row = source as DataGridRow ?? source.FindAncestorOfType<DataGridRow>();
        if (row?.DataContext is not Quotation selected || selected.Id <= 0) return;
        if (!ReferenceEquals(QuoteHistoryGrid.SelectedItem, selected)) return;
        e.Handled = true;

        _openingQuotation = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            var quotation = await service.GetQuotationByIdAsync(selected.Id);
            if (_stopped || !QuoteHistoryPanel.IsVisible) return;
            if (quotation is null || quotation.CustomerId != CustomerId)
            {
                await LoadQuoteHistoryAsync();
                QuoteHistoryStatus.Text = "This quotation no longer belongs to this customer or has been deleted. History refreshed.";
                return;
            }

            if (OpenQuotationRequested is { } open)
                await open(quotation);
        }
        catch (Exception ex)
        {
            if (!_stopped)
                QuoteHistoryStatus.Text = $"Could not open quotation: {ex.Message}";
        }
        finally
        {
            _openingQuotation = false;
        }
    }
    private void OrderHistory_Click(object? sender, RoutedEventArgs e) => ShowHistory("Order History");

    private void NewContact_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddContact();
        ShowContacts();
    }

    private void DeleteContact_Click(object? sender, RoutedEventArgs e)
        => ViewModel.DeleteSelectedContact();

    private void SelectLineGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private void ShowAddress()
    {
        AddressPanel.IsVisible = true;
        ContactPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        QuoteHistoryPanel.IsVisible = false;
        _quoteHistoryLoadVersion++;
    }

    private void ShowContacts()
    {
        AddressPanel.IsVisible = false;
        ContactPanel.IsVisible = true;
        HistoryPanel.IsVisible = false;
        QuoteHistoryPanel.IsVisible = false;
        _quoteHistoryLoadVersion++;
    }

    private void ShowHistory(string title)
    {
        AddressPanel.IsVisible = false;
        ContactPanel.IsVisible = false;
        QuoteHistoryPanel.IsVisible = false;
        _quoteHistoryLoadVersion++;
        HistoryPanel.IsVisible = true;
        HistoryTitle.Text = title;
    }
}
