using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.PackingLists;
using MiniERP.Desktop.Views.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.PackingLists;

public partial class PackingListEditorView : UserControl
{
    private readonly EditorDirtyMonitor _dirtyMonitor;
    private PackingListEditorViewModel ViewModel => (PackingListEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;

    public int PackingListId => ViewModel.PackingList.Id;

    public PackingListEditorView(PackingList? packingList)
    {
        InitializeComponent();
        DataContext = new PackingListEditorViewModel(packingList);

        var saveButton = EditorWorkflowSupport.AddGridViewToggle(this, GridView_Click);
        _dirtyMonitor = new EditorDirtyMonitor(saveButton, CaptureEditState);
        AttachedToVisualTree += async (_, _) =>
        {
            await ViewModel.LoadLookupsAsync();
            _dirtyMonitor.Start();
        };
    }

    public void StopTracking() => _dirtyMonitor.Dispose();

    private string CaptureEditState()
        => EditorWorkflowSupport.Snapshot(new
        {
            ViewModel.PackingList,
            CustomerId = ViewModel.SelectedCustomer?.Id,
            ContactId = ViewModel.SelectedCustomerContact?.Id,
            UserId = ViewModel.SelectedUser?.Id,
            ViewModel.PackingDate,
            ViewModel.CustomerPoDate,
            Items = ViewModel.Items.ToArray(),
            Packages = ViewModel.Packages.ToArray()
        });

    private void GridView_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private void AddItem_Click(object? sender, RoutedEventArgs e) => ViewModel.AddSelectedArticle();
    private void RemoveItem_Click(object? sender, RoutedEventArgs e) => ViewModel.RemoveSelectedItem();
    private void AddPackage_Click(object? sender, RoutedEventArgs e) => ViewModel.AddPackage();
    private void RemovePackage_Click(object? sender, RoutedEventArgs e) => ViewModel.RemoveSelectedPackage();

    private void MoveItemUp_Click(object? sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item is null)
        {
            ViewModel.SetStatusMessage("Select a packing-list item first.");
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
        ViewModel.SetStatusMessage("Packing-list item moved up. Save to persist the new order.");
    }

    private void MoveItemDown_Click(object? sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item is null)
        {
            ViewModel.SetStatusMessage("Select a packing-list item first.");
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
        ViewModel.SetStatusMessage("Packing-list item moved down. Save to persist the new order.");
    }

    private void MovePackageUp_Click(object? sender, RoutedEventArgs e)
    {
        var package = ViewModel.SelectedPackage;
        if (package is null)
        {
            ViewModel.SetStatusMessage("Select a package row first.");
            return;
        }

        var index = ViewModel.Packages.IndexOf(package);
        if (index <= 0)
        {
            ViewModel.SetStatusMessage("The selected package is already first.");
            return;
        }

        ViewModel.SelectedPackage = null;
        ViewModel.Packages.RemoveAt(index);
        ViewModel.Packages.Insert(index - 1, package);
        ViewModel.SelectedPackage = package;
        ViewModel.SetStatusMessage("Package row moved up. Save to persist the new order.");
    }

    private void MovePackageDown_Click(object? sender, RoutedEventArgs e)
    {
        var package = ViewModel.SelectedPackage;
        if (package is null)
        {
            ViewModel.SetStatusMessage("Select a package row first.");
            return;
        }

        var index = ViewModel.Packages.IndexOf(package);
        if (index < 0 || index >= ViewModel.Packages.Count - 1)
        {
            ViewModel.SetStatusMessage("The selected package is already last.");
            return;
        }

        ViewModel.SelectedPackage = null;
        ViewModel.Packages.RemoveAt(index);
        ViewModel.Packages.Insert(index + 1, package);
        ViewModel.SelectedPackage = package;
        ViewModel.SetStatusMessage("Package row moved down. Save to persist the new order.");
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
        if (!ViewModel.TryPrepareForExport()) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null) { ViewModel.SetStatusMessage("PDF export is not available on this desktop session."); return; }
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Packing List PDF",
            SuggestedFileName = $"{SanitizeFileName(ViewModel.PackingList.PackingListNumber)}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF document") { Patterns = new[] { "*.pdf" } } }
        });
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            SalesDocumentPdfExporter.ExportPackingList(ViewModel.PackingList, stream);
            await stream.FlushAsync();
            ViewModel.SetStatusMessage("PDF exported.");
        }
        catch (Exception ex) { ViewModel.SetStatusMessage($"PDF export failed: {ex.Message}"); }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync()) return;
        _dirtyMonitor.MarkClean();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Discard_Click(object? sender, RoutedEventArgs e) => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsNew)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(this, "Delete Packing List", $"Delete '{ViewModel.PackingList.PackingListNumber}'? This cannot be undone.");
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
        return string.IsNullOrWhiteSpace(sanitized) ? "PackingList" : sanitized;
    }
}
