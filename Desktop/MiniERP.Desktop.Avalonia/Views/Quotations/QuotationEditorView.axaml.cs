using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView : UserControl
{
    private QuotationEditorViewModel ViewModel => (QuotationEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;

    public QuotationEditorView(Quotation? quotation)
    {
        InitializeComponent();
        var settings = App.Services.GetRequiredService<AppSettingsService>();
        DataContext = new QuotationEditorViewModel(quotation, settings);
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadLookupsAsync();
    }

    public void RefreshExchangeRate() => ViewModel.RefreshExchangeRateFromSettings();
    private void AddItem_Click(object? sender, RoutedEventArgs e) => ViewModel.AddSelectedArticle();
    private void RemoveItem_Click(object? sender, RoutedEventArgs e) => ViewModel.RemoveSelectedItem();

    private async void PickCustomer_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var picker = new CustomerPickerWindow(
            ViewModel.Customers,
            ViewModel.SelectedCustomer?.Id);

        var selected = await picker.ShowDialog<Customer?>(owner);
        if (selected is not null)
        {
            ViewModel.SelectedCustomer = selected;
            ViewModel.SetStatusMessage($"Customer selected: {selected.Name}");
        }
    }

    private async void ExportPdf_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryPrepareForExport())
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            ViewModel.SetStatusMessage("PDF export is not available on this desktop session.");
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Quotation PDF",
            SuggestedFileName = $"{SanitizeFileName(ViewModel.Quotation.QuotationNumber)}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } }
            }
        });

        if (file is null)
            return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            QuotationPdfExporter.Export(ViewModel.Quotation, stream);
            await stream.FlushAsync();
            ViewModel.SetStatusMessage("PDF exported.");
        }
        catch (Exception ex)
        {
            ViewModel.SetStatusMessage($"PDF export failed: {ex.Message}");
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync()) return;
        Saved?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
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
