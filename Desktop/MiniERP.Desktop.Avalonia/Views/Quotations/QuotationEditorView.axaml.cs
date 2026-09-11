using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView : UserControl
{
    private readonly EditorDirtyMonitor _dirtyMonitor;
    private QuotationItemRowViewModel? _editingPosition;
    private bool _loadingPositionEditor;
    private QuotationEditorViewModel ViewModel => (QuotationEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;
    public event Action<Quotation, InvoiceType>? CreateInvoiceRequested;
    public event Action<Quotation>? CreatePackingListRequested;
    public event Action<Quotation>? CreateContractRequested;

    public int QuotationId => ViewModel.Quotation.Id;

    public QuotationEditorView(Quotation? quotation)
    {
        InitializeComponent();

        // Fluent buttons are vertically centered by default. Inside a lookup field that
        // leaves visible gaps above and below the picker, making it look like a separate
        // control. Stretch the picker through the full input height and let the outer
        // lookup border provide the only rounded outline.
        foreach (var lookupButton in this.GetLogicalDescendants()
                     .OfType<Button>()
                     .Where(button => button.Classes.Contains("lookup-button")))
        {
            lookupButton.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            lookupButton.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            lookupButton.CornerRadius = new Avalonia.CornerRadius(0);
            lookupButton.MinHeight = 0;
        }

        // Keep the empty staged editor visually consistent with the rest of the ERP.
        // There is no editing target until a position is double-clicked, but disabling
        // the whole panel makes Fluent render every input with a heavy gray fill.
        PositionEditorPanel.IsEnabled = true;

        // Amount is a read-only TextBox. Keep its minimum height aligned with the other
        // position inputs even when it wraps onto a line by itself.
        PositionAmountText.MinHeight = 32;

        var settings = App.Services.GetRequiredService<AppSettingsService>();
        DataContext = new QuotationEditorViewModel(quotation, settings);

        var saveButton = EditorWorkflowSupport.AddGridViewToggle(this, GridView_Click);
        _dirtyMonitor = new EditorDirtyMonitor(saveButton, CaptureEditState);
        AttachedToVisualTree += async (_, _) =>
        {
            await ViewModel.LoadLookupsAsync();
            _dirtyMonitor.Start();
        };
    }

    public void StopTracking() => _dirtyMonitor.Dispose();
    public void RefreshExchangeRate() => ViewModel.RefreshExchangeRateFromSettings();

    private string CaptureEditState()
        => EditorWorkflowSupport.Snapshot(new
        {
            ViewModel.Quotation,
            CustomerId = ViewModel.SelectedCustomer?.Id,
            ContactId = ViewModel.SelectedCustomerContact?.Id,
            UserId = ViewModel.SelectedUser?.Id,
            ViewModel.QuotationDate,
            ViewModel.ValidUntil,
            ViewModel.Currency,
            ViewModel.ExchangeRateSnapshot,
            Items = ViewModel.Items.ToArray()
        });

    private void GridView_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private void AddItem_Click(object? sender, RoutedEventArgs e)
        => ViewModel.AddSelectedArticle();

    private void RemoveItem_Click(object? sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        ViewModel.RemoveSelectedItem();
        if (item is not null && ReferenceEquals(item, _editingPosition))
            ClearPositionEditor();
    }

    private void MoveItemUp_Click(object? sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item is null) { ViewModel.SetStatusMessage("Select a quotation item first."); return; }
        var index = ViewModel.Items.IndexOf(item);
        if (index <= 0) { ViewModel.SetStatusMessage("The selected item is already first."); return; }
        ViewModel.SelectedItem = null;
        ViewModel.Items.RemoveAt(index);
        ViewModel.Items.Insert(index - 1, item);
        ViewModel.SelectedItem = item;
        ViewModel.SetStatusMessage("Quotation item moved up. Save the document to persist the new order.");
    }

    private void MoveItemDown_Click(object? sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item is null) { ViewModel.SetStatusMessage("Select a quotation item first."); return; }
        var index = ViewModel.Items.IndexOf(item);
        if (index < 0 || index >= ViewModel.Items.Count - 1) { ViewModel.SetStatusMessage("The selected item is already last."); return; }
        ViewModel.SelectedItem = null;
        ViewModel.Items.RemoveAt(index);
        ViewModel.Items.Insert(index + 1, item);
        ViewModel.SelectedItem = item;
        ViewModel.SetStatusMessage("Quotation item moved down. Save the document to persist the new order.");
    }

    private void PositionsGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item is null)
            return;

        if (_editingPosition is not null &&
            !ReferenceEquals(_editingPosition, item) &&
            PositionSaveButton.IsEnabled)
        {
            ViewModel.SelectedItem = _editingPosition;
            ViewModel.SetStatusMessage("Save the current position changes before opening another position.");
            return;
        }

        LoadPositionEditor(item);
        ViewModel.SetStatusMessage($"Editing position: {item.ArticleName}");
    }

    private void LoadPositionEditor(QuotationItemRowViewModel item)
    {
        _loadingPositionEditor = true;
        try
        {
            _editingPosition = item;
            PositionArticleTextBox.Text = item.ArticleName;
            PositionQtyTextBox.Text = item.QuantityText;
            PositionUnitTextBox.Text = item.Unit;
            PositionUnitPriceTextBox.Text = item.UnitPriceText;
            PositionDiscountTextBox.Text = item.DiscountText;
            PositionDescriptionTextBox.Text = item.Description ?? string.Empty;
            PositionAmountText.Text = item.LineTotal.ToString("N2", CultureInfo.InvariantCulture);
            PositionEditorPanel.IsEnabled = true;
            PositionSaveButton.IsEnabled = false;
        }
        finally
        {
            _loadingPositionEditor = false;
        }
    }

    private void ClearPositionEditor()
    {
        _loadingPositionEditor = true;
        try
        {
            _editingPosition = null;
            PositionArticleTextBox.Text = string.Empty;
            PositionQtyTextBox.Text = string.Empty;
            PositionUnitTextBox.Text = string.Empty;
            PositionUnitPriceTextBox.Text = string.Empty;
            PositionDiscountTextBox.Text = string.Empty;
            PositionDescriptionTextBox.Text = string.Empty;
            PositionAmountText.Text = string.Empty;

            // Keep the empty editor enabled so it retains the normal white ERP input
            // appearance. Without an editing target TextChanged is ignored below.
            PositionEditorPanel.IsEnabled = true;
            PositionSaveButton.IsEnabled = false;
        }
        finally
        {
            _loadingPositionEditor = false;
        }
    }

    private void PositionEditor_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loadingPositionEditor || _editingPosition is null)
            return;

        UpdatePositionAmountPreview();
        PositionSaveButton.IsEnabled = !PositionEditorMatchesCurrentItem();
    }

    private void SavePositionEdit_Click(object? sender, RoutedEventArgs e)
    {
        if (_editingPosition is null)
        {
            ViewModel.SetStatusMessage("Double-click a position before editing it.");
            return;
        }

        var articleName = (PositionArticleTextBox.Text ?? string.Empty).Trim();
        var quantityText = NormalizeDecimalInput(PositionQtyTextBox.Text);
        var unit = (PositionUnitTextBox.Text ?? string.Empty).Trim();
        var unitPriceText = NormalizeDecimalInput(PositionUnitPriceTextBox.Text);
        var discountText = NormalizeDecimalInput(PositionDiscountTextBox.Text);

        if (string.IsNullOrWhiteSpace(articleName))
        {
            ViewModel.SetStatusMessage("The position article name is required.");
            return;
        }

        if (!TryParseDecimal(quantityText, out var quantity) || quantity <= 0)
        {
            ViewModel.SetStatusMessage($"Quantity for '{articleName}' must be greater than zero.");
            return;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            ViewModel.SetStatusMessage($"Unit for '{articleName}' is required.");
            return;
        }

        if (!TryParseDecimal(unitPriceText, out var unitPrice) || unitPrice < 0)
        {
            ViewModel.SetStatusMessage($"Unit price for '{articleName}' cannot be negative.");
            return;
        }

        if (!TryParseDecimal(discountText, out var discount) || discount < 0 || discount > 100)
        {
            ViewModel.SetStatusMessage($"Discount for '{articleName}' must be between 0 and 100%.");
            return;
        }

        _editingPosition.ArticleName = articleName;
        _editingPosition.QuantityText = quantityText;
        _editingPosition.Unit = unit;
        _editingPosition.UnitPriceText = unitPriceText;
        _editingPosition.DiscountText = discountText;
        _editingPosition.Description = PositionDescriptionTextBox.Text;

        ViewModel.SelectedItem = _editingPosition;
        var savedName = _editingPosition.ArticleName;
        LoadPositionEditor(_editingPosition);
        ViewModel.SetStatusMessage($"Position '{savedName}' updated in the overview. Save the document to persist it.");
    }

    private void UpdatePositionAmountPreview()
    {
        var quantity = ParseDecimalOrZero(PositionQtyTextBox.Text);
        var unitPrice = ParseDecimalOrZero(PositionUnitPriceTextBox.Text);
        var discount = ParseDecimalOrZero(PositionDiscountTextBox.Text);
        var amount = decimal.Round(quantity * unitPrice * (1m - discount / 100m), 2, MidpointRounding.AwayFromZero);
        PositionAmountText.Text = amount.ToString("N2", CultureInfo.InvariantCulture);
    }

    private bool PositionEditorMatchesCurrentItem()
    {
        if (_editingPosition is null)
            return true;

        return string.Equals(PositionArticleTextBox.Text ?? string.Empty, _editingPosition.ArticleName, StringComparison.Ordinal)
            && string.Equals(NormalizeDecimalInput(PositionQtyTextBox.Text), _editingPosition.QuantityText, StringComparison.Ordinal)
            && string.Equals(PositionUnitTextBox.Text ?? string.Empty, _editingPosition.Unit, StringComparison.Ordinal)
            && string.Equals(NormalizeDecimalInput(PositionUnitPriceTextBox.Text), _editingPosition.UnitPriceText, StringComparison.Ordinal)
            && string.Equals(NormalizeDecimalInput(PositionDiscountTextBox.Text), _editingPosition.DiscountText, StringComparison.Ordinal)
            && string.Equals(PositionDescriptionTextBox.Text ?? string.Empty, _editingPosition.Description ?? string.Empty, StringComparison.Ordinal);
    }

    private bool EnsurePositionEditApplied()
    {
        if (_editingPosition is null || !PositionSaveButton.IsEnabled)
            return true;

        ViewModel.SetStatusMessage("The selected position has unapplied changes. Click the Position Save button first.");
        return false;
    }

    private static string NormalizeDecimalInput(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = new System.Text.StringBuilder();
        var hasSeparator = false;
        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                result.Append(character);
                continue;
            }

            if ((character == '.' || character == ',') && !hasSeparator)
            {
                result.Append('.');
                hasSeparator = true;
            }
        }

        if (result.Length > 0 && result[0] == '.')
            result.Insert(0, '0');

        return result.ToString();
    }

    private static bool TryParseDecimal(string? value, out decimal result)
        => decimal.TryParse(NormalizeDecimalInput(value), NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static decimal ParseDecimalOrZero(string? value)
        => TryParseDecimal(value, out var parsed) ? parsed : 0m;

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
        if (!EnsurePositionEditApplied()) return;
        if (!ViewModel.TryPrepareForExport()) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) { ViewModel.SetStatusMessage("PDF export is not available on this desktop session."); return; }
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Quotation PDF",
            SuggestedFileName = $"{SanitizeFileName(ViewModel.Quotation.QuotationNumber)}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } } }
        });
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            QuotationPdfExporter.Export(ViewModel.Quotation, stream);
            await stream.FlushAsync();
            ViewModel.SetStatusMessage("PDF exported.");
        }
        catch (Exception ex) { ViewModel.SetStatusMessage($"PDF export failed: {ex.Message}"); }
    }

    private async void CreatePi_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditApplied()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        CreateInvoiceRequested?.Invoke(ViewModel.Quotation, InvoiceType.Proforma);
    }

    private async void CreateInvoice_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditApplied()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        CreateInvoiceRequested?.Invoke(ViewModel.Quotation, InvoiceType.Commercial);
    }

    private async void CreatePackingList_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditApplied()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        CreatePackingListRequested?.Invoke(ViewModel.Quotation);
    }

    private async void CreateContract_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditApplied()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
        CreateContractRequested?.Invoke(ViewModel.Quotation);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!EnsurePositionEditApplied()) return;
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Discard_Click(object? sender, RoutedEventArgs e) => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsNew)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Quotation", $"Delete quotation '{ViewModel.Quotation.QuotationNumber}'? This cannot be undone.");
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
        return string.IsNullOrWhiteSpace(sanitized) ? "Quotation" : sanitized;
    }
}
