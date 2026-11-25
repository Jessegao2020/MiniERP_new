using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
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
    private enum HistoryKind { Quotation, Proforma, Contract, Invoice, PackingList }

    private HistoryKind? _historyKind;
    private int _historyLoadVersion;
    private bool _openingDocument;
    private bool _stopped;
    private CustomerEditorViewModel ViewModel => (CustomerEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;
    public event Func<object, Task>? OpenHistoryDocumentRequested;

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
            if (DocumentHistoryPanel.IsVisible)
                await LoadDocumentHistoryAsync();
        };
    }

    public void StopTracking()
    {
        _stopped = true;
        _historyLoadVersion++;
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
        if (DocumentHistoryPanel.IsVisible)
            await LoadDocumentHistoryAsync();
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
        => await ShowHistoryAsync(HistoryKind.Quotation);
    private async void ProformaHistory_Click(object? sender, RoutedEventArgs e)
        => await ShowHistoryAsync(HistoryKind.Proforma);
    private async void ContractHistory_Click(object? sender, RoutedEventArgs e)
        => await ShowHistoryAsync(HistoryKind.Contract);
    private async void InvoiceHistory_Click(object? sender, RoutedEventArgs e)
        => await ShowHistoryAsync(HistoryKind.Invoice);
    private async void PackingListHistory_Click(object? sender, RoutedEventArgs e)
        => await ShowHistoryAsync(HistoryKind.PackingList);

    private static string HistoryTitle(HistoryKind kind) => kind switch
    {
        HistoryKind.Quotation => "Quotation",
        HistoryKind.Proforma => "P/I",
        HistoryKind.Contract => "Contract",
        HistoryKind.Invoice => "Invoice",
        HistoryKind.PackingList => "P/L",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private async Task ShowHistoryAsync(HistoryKind kind)
    {
        if (_stopped) return;
        AddressPanel.IsVisible = false;
        ContactPanel.IsVisible = false;
        DocumentHistoryPanel.IsVisible = true;
        if (_historyKind != kind)
        {
            DocumentHistoryGrid.SelectedItem = null;
            DocumentHistoryGrid.ItemsSource = Array.Empty<object>();
            ConfigureHistoryColumns(kind);
            _historyKind = kind;
        }
        DocumentHistoryTitle.Text = HistoryTitle(kind) + " History";
        await LoadDocumentHistoryAsync();
    }

    private void ConfigureHistoryColumns(HistoryKind kind)
    {
        DocumentHistoryGrid.Columns.Clear();
        void Add(string header, string path, double width = 1, string? format = null)
        {
            var binding = new Binding(path);
            if (format is not null) binding.StringFormat = format;
            DocumentHistoryGrid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = binding,
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            });
        }

        var (number, date) = kind switch
        {
            HistoryKind.Quotation => ("QuotationNumber", "QuotationDate"),
            HistoryKind.Proforma or HistoryKind.Invoice => ("InvoiceNumber", "InvoiceDate"),
            HistoryKind.Contract => ("ContractNumber", "ContractDate"),
            HistoryKind.PackingList => ("PackingListNumber", "PackingDate"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Add(HistoryTitle(kind) + " No.", number, 1.6);
        Add("Date", date, 1, "{0:yyyy-MM-dd}");
        Add("User", "User.Name", 1.2);
        if (kind == HistoryKind.PackingList)
        {
            Add("Quantity", "TotalQuantity", 1, "{0:0.##}");
            Add("Cartons", "TotalCartons", 0.8);
            Add("Net Weight (kg)", "TotalNetWeight", 1.2, "{0:N2}");
            Add("Gross Weight (kg)", "TotalGrossWeight", 1.2, "{0:N2}");
            Add("CBM", "TotalCbm", 0.8, "{0:0.####}");
        }
        else
        {
            Add("Currency", "Currency", 0.7);
            Add("Total", "TotalAmount", 1, "{0:N2}");
            if (kind == HistoryKind.Quotation)
                Add("Valid Until", "ValidUntil", 1, "{0:yyyy-MM-dd}");
            else
            {
                Add("Customer PO", "CustomerPoNumber", 1.2);
                if (kind is HistoryKind.Proforma or HistoryKind.Invoice)
                    Add("Due Date", "DueDate", 1, "{0:yyyy-MM-dd}");
            }
        }
        Add("Delivery Term", "DeliveryTerm", 1.2);
        if (kind is HistoryKind.Quotation or HistoryKind.Contract)
            Add("Lead Time", "LeadTime", 1.2);
        if (kind != HistoryKind.PackingList)
            Add("Payment Term", "PaymentTerm", 1.4);
        if (kind != HistoryKind.Quotation)
            Add("Source Document", "SourceDocumentNumber", 1.3);
    }

    private async Task LoadDocumentHistoryAsync()
    {
        if (_stopped || _historyKind is not { } kind || !DocumentHistoryPanel.IsVisible) return;
        var version = ++_historyLoadVersion;
        var customerId = CustomerId;
        DocumentHistoryGrid.SelectedItem = null;
        DocumentHistoryGrid.ItemsSource = Array.Empty<object>();

        if (customerId <= 0)
        {
            DocumentHistoryStatus.Text = "Save the customer before viewing document history.";
            return;
        }

        DocumentHistoryStatus.Text = "Loading document history...";
        try
        {
            using var scope = App.Services.CreateScope();
            var services = scope.ServiceProvider;
            IEnumerable<object> rows = kind switch
            {
                HistoryKind.Quotation => (await services.GetRequiredService<IQuotationService>()
                    .GetQuotationsByCustomerIdAsync(customerId))
                    .OrderByDescending(q => q.QuotationDate).ThenByDescending(q => q.Id),
                HistoryKind.Proforma or HistoryKind.Invoice => (await services.GetRequiredService<IInvoiceService>()
                    .GetInvoicesByCustomerIdAsync(customerId))
                    .Where(i => i.Type == (kind == HistoryKind.Proforma ? InvoiceType.Proforma : InvoiceType.Commercial))
                    .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id),
                HistoryKind.Contract => (await services.GetRequiredService<IContractService>()
                    .GetContractsByCustomerIdAsync(customerId))
                    .OrderByDescending(d => d.ContractDate).ThenByDescending(d => d.Id),
                HistoryKind.PackingList => (await services.GetRequiredService<IPackingListService>()
                    .GetPackingListsByCustomerIdAsync(customerId))
                    .OrderByDescending(d => d.PackingDate).ThenByDescending(d => d.Id),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            var documents = rows.ToList();
            if (_stopped || version != _historyLoadVersion || customerId != CustomerId) return;
            DocumentHistoryGrid.ItemsSource = documents;
            DocumentHistoryStatus.Text = documents.Count == 0
                ? "No documents for this customer in this category."
                : $"{documents.Count} document(s). Double-click a row to open it.";
        }
        catch (Exception ex)
        {
            if (!_stopped && version == _historyLoadVersion)
                DocumentHistoryStatus.Text = $"Could not load document history: {ex.Message}";
        }
    }

    private async void DocumentHistoryGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_stopped || _openingDocument || _historyKind is not { } kind || e.Source is not Visual source) return;
        var row = source as DataGridRow ?? source.FindAncestorOfType<DataGridRow>();
        var selected = row?.DataContext;
        if (selected is null || !ReferenceEquals(DocumentHistoryGrid.SelectedItem, selected)) return;
        e.Handled = true;

        _openingDocument = true;
        try
        {
            var customerId = CustomerId;
            using var scope = App.Services.CreateScope();
            var services = scope.ServiceProvider;
            object? document = selected switch
            {
                Quotation q when kind == HistoryKind.Quotation =>
                    await services.GetRequiredService<IQuotationService>().GetQuotationByIdAsync(q.Id),
                Invoice i when kind is HistoryKind.Proforma or HistoryKind.Invoice =>
                    await services.GetRequiredService<IInvoiceService>().GetInvoiceByIdAsync(i.Id),
                Contract d when kind == HistoryKind.Contract =>
                    await services.GetRequiredService<IContractService>().GetContractByIdAsync(d.Id),
                PackingList d when kind == HistoryKind.PackingList =>
                    await services.GetRequiredService<IPackingListService>().GetPackingListByIdAsync(d.Id),
                _ => null
            };
            if (_stopped || !DocumentHistoryPanel.IsVisible || _historyKind != kind
                || CustomerId != customerId || TopLevel.GetTopLevel(this) is null) return;

            var matches = document switch
            {
                Quotation q => kind == HistoryKind.Quotation && q.CustomerId == customerId,
                Invoice i => (kind == HistoryKind.Proforma || kind == HistoryKind.Invoice)
                    && i.CustomerId == customerId
                    && i.Type == (kind == HistoryKind.Proforma ? InvoiceType.Proforma : InvoiceType.Commercial),
                Contract d => kind == HistoryKind.Contract && d.CustomerId == customerId,
                PackingList d => kind == HistoryKind.PackingList && d.CustomerId == customerId,
                _ => false
            };
            if (!matches || document is null)
            {
                await LoadDocumentHistoryAsync();
                return;
            }

            if (OpenHistoryDocumentRequested is { } open)
                await open(document);
        }
        catch (Exception ex)
        {
            if (!_stopped && _historyKind == kind)
                DocumentHistoryStatus.Text = $"Could not open document: {ex.Message}";
        }
        finally
        {
            _openingDocument = false;
        }
    }

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
        DocumentHistoryPanel.IsVisible = false;
        _historyLoadVersion++;
    }

    private void ShowContacts()
    {
        AddressPanel.IsVisible = false;
        ContactPanel.IsVisible = true;
        DocumentHistoryPanel.IsVisible = false;
        _historyLoadVersion++;
    }
}
