using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Invoices;
using MiniERP.Desktop.Views.Quotations;
using MiniERP.Domain;
using MiniERP.ApplicationLayer.Services;

namespace MiniERP.Desktop.Views.Invoices;

public partial class InvoiceEditorView : UserControl
{
    private readonly EditorDirtyMonitor _dirtyMonitor;
    private InvoiceEditorViewModel ViewModel => (InvoiceEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;
    public event Action<Invoice>? CreateCommercialRequested;
    public event Action<Invoice>? CreatePackingListRequested;
    public event Action<Invoice>? CreateContractRequested;

    public int InvoiceId => ViewModel.Invoice.Id;

    public bool HasUnsavedChanges => _dirtyMonitor.HasUnsavedChangesNow() || (HasUnappliedPositionChanges());

    public InvoiceEditorView(Invoice? invoice, InvoiceType type)
    {
        InitializeComponent();
        var settings = App.Services.GetRequiredService<AppSettingsService>();
        DataContext = new InvoiceEditorViewModel(invoice, type, settings);
        CreateCommercialButton.IsVisible = type == InvoiceType.Proforma;
        CurrencySelector.ItemsSource = ViewModel.Currencies;
        CurrencySelector.SelectedItem = ViewModel.Currency;
        CurrencySelector.SelectionChanged += CurrencySelector_SelectionChanged;
        InitializePositionEditor();

        var saveButton = EditorWorkflowSupport.AddGridViewToggle(this, GridView_Click);
        _dirtyMonitor = new EditorDirtyMonitor(saveButton, CaptureEditState);
        AttachedToVisualTree += async (_, _) =>
        {
            // Configure the existing position controls once their templates are available.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ConfigurePositionEditor();
                NormalizePickerButtons();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
            await ViewModel.LoadLookupsAsync();
            _dirtyMonitor.Start();
        };
    }

    public void StopTracking() => _dirtyMonitor.Dispose();

    private string CaptureEditState()
        => EditorWorkflowSupport.Snapshot(new
        {
            ViewModel.Invoice,
            CustomerId = ViewModel.SelectedCustomer?.Id,
            ContactId = ViewModel.SelectedCustomerContact?.Id,
            UserId = ViewModel.SelectedUser?.Id,
            ViewModel.InvoiceDate,
            ViewModel.DueDate,
            ViewModel.CustomerPoDate,
            ViewModel.Currency,
            ViewModel.ExchangeRateSnapshot,
            Items = ViewModel.Items.ToArray()
        });

    private bool _currencyChangeInProgress;

    private async void CurrencySelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_currencyChangeInProgress || DataContext is not InvoiceEditorViewModel vm ||
            CurrencySelector.SelectedItem is not string target || target == vm.Currency) return;
        _currencyChangeInProgress = true;
        try
        {
            // Keep the displayed selection unchanged while the decision is pending.
            CurrencySelector.SelectedItem = vm.Currency;
            CurrencySelector.IsEnabled = false;
            if (!EnsurePositionEditorCommitted()) return;
            using var scope = App.Services.CreateScope();
            var guard = scope.ServiceProvider.GetRequiredService<IDocumentCurrencyGuard>();
            if (!vm.IsNew && await guard.HasDownstreamAsync(ViewModel.Invoice.Type == InvoiceType.Proforma ? DocumentSourceType.ProformaInvoice : DocumentSourceType.CommercialInvoice, vm.Invoice.Id))
            {
                vm.SetStatusMessage("Cannot change currency: this document has downstream sales documents. Create a new document or revision instead.");
                return;
            }
            var lines = vm.GetCurrencyConversionLines();
            var rate = vm.CurrencyConversionRate;
            CurrencyConversionPlan? plan;
            if (lines.Count == 0)
            {
                if (!CurrencyConversionService.TryPlan(vm.Currency, target, rate,
                    CurrencyConversionMode.ConvertExistingPrices, lines, Array.Empty<Article>(), out plan, out var error))
                { vm.SetStatusMessage(error); return; }
            }
            else
            {
                // Fresh catalog values are needed only for the optional catalog mode.
                var catalog = (await scope.ServiceProvider.GetRequiredService<IArticleService>().GetAllArticlesAsync()).ToList();
                plan = await CurrencyConversionDialog.ShowAsync(this, vm.Currency, target, rate, lines, catalog, !vm.IsNew);
            }
            if (plan is null) return;
            vm.ApplyCurrencyConversion(plan);
            if (_editingPosition is not null) LoadPositionEditor(_editingPosition);
        }
        catch (Exception ex) { vm.SetStatusMessage($"Currency change failed: {ex.Message}"); }
        finally
        {
            CurrencySelector.SelectedItem = vm.Currency;
            CurrencySelector.IsEnabled = true;
            _currencyChangeInProgress = false;
        }
    }

    private void GridView_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private void AddItem_Click(object? sender, RoutedEventArgs e) => ViewModel.AddSelectedArticle();
    private void RemoveItem_Click(object? sender, RoutedEventArgs e) => ViewModel.RemoveSelectedItem();

    private void MoveItemUp_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditorCommitted()) return;
        var item = ViewModel.SelectedItem;
        if (item is null)
        {
            ViewModel.SetStatusMessage("Select an invoice item first.");
            return;
        }

        var index = ViewModel.Items.IndexOf(item);
        if (index <= 0)
        {
            ViewModel.SetStatusMessage("The selected item is already first.");
            return;
        }

        ViewModel.SelectedItem = null;
        ViewModel.Items.RemoveAt(index);
        ViewModel.Items.Insert(index - 1, item);
        ViewModel.SelectedItem = item;
        ViewModel.SetStatusMessage("Invoice item moved up. Save to persist the new order.");
    }

    private void MoveItemDown_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditorCommitted()) return;
        var item = ViewModel.SelectedItem;
        if (item is null)
        {
            ViewModel.SetStatusMessage("Select an invoice item first.");
            return;
        }

        var index = ViewModel.Items.IndexOf(item);
        if (index < 0 || index >= ViewModel.Items.Count - 1)
        {
            ViewModel.SetStatusMessage("The selected item is already last.");
            return;
        }

        ViewModel.SelectedItem = null;
        ViewModel.Items.RemoveAt(index);
        ViewModel.Items.Insert(index + 1, item);
        ViewModel.SelectedItem = item;
        ViewModel.SetStatusMessage("Invoice item moved down. Save to persist the new order.");
    }

    private async void PickCustomer_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var picker = new CustomerPickerWindow(ViewModel.Customers, ViewModel.SelectedCustomer?.Id);
        var selected = await picker.ShowDialog<Customer?>(owner);
        if (selected is not null) { ViewModel.SelectedCustomer = selected; ViewModel.SetStatusMessage($"Customer selected: {selected.Name}"); }
    }

    private async void PickContact_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (ViewModel.SelectedCustomer is null) { ViewModel.SetStatusMessage("Select a customer before choosing a contact."); return; }
        var picker = new ContactPickerWindow(ViewModel.CustomerContacts, ViewModel.SelectedCustomerContact?.Id);
        var selected = await picker.ShowDialog<CustomerContact?>(owner);
        if (selected is not null) { ViewModel.SelectedCustomerContact = selected; ViewModel.SetStatusMessage($"Contact selected: {selected.Name}"); }
    }

    private async void PickArticle_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var picker = new ArticlePickerWindow(ViewModel.Articles, ViewModel.SelectedArticle?.Id);
        var selected = await picker.ShowDialog<Article?>(owner);
        if (selected is not null) { ViewModel.SelectedArticle = selected; ViewModel.SetStatusMessage($"Article selected: {selected.Name}"); }
    }

    private void SelectLineGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
        => SelectLineGridSupport.ApplyAlternateRow(e);

    private async void ExportPdf_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditorCommitted()) return;
        if (!ViewModel.TryPrepareForExport()) return;
        // Like Quotation: never export a document that has not been persisted.
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) { ViewModel.SetStatusMessage("PDF export is not available on this desktop session."); return; }
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export {ViewModel.DocumentTitle} PDF",
            SuggestedFileName = $"{SanitizeFileName(ViewModel.Invoice.InvoiceNumber)}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } } }
        });
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            SalesDocumentPdfExporter.ExportInvoice(ViewModel.Invoice, stream);
            await stream.FlushAsync();
            ViewModel.SetStatusMessage("PDF exported.");
        }
        catch (Exception ex) { ViewModel.SetStatusMessage($"PDF export failed: {ex.Message}"); }
    }

    private async void CreateCommercial_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Invoice.Type != InvoiceType.Proforma) return;
        if (!EnsurePositionEditorCommitted()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        CreateCommercialRequested?.Invoke(ViewModel.Invoice);
    }

    private async void CreatePackingList_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditorCommitted()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        CreatePackingListRequested?.Invoke(ViewModel.Invoice);
    }

    private async void CreateContract_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditorCommitted()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        CreateContractRequested?.Invoke(ViewModel.Invoice);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditorCommitted()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Discard_Click(object? sender, RoutedEventArgs e) => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsNew)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(this, $"Delete {ViewModel.DocumentTitle}", $"Delete '{ViewModel.Invoice.InvoiceNumber}'? This cannot be undone.");
            if (!confirmed) return;
        }
        if (!await ViewModel.DeleteAsync()) return;
        Deleted?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Invoice" : sanitized;
    }
}
