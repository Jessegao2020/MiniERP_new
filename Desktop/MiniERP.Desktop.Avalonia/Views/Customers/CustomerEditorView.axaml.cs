using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Customers;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

public partial class CustomerEditorView : UserControl
{
    private readonly EditorDirtyMonitor _dirtyMonitor;
    private CustomerEditorViewModel ViewModel => (CustomerEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;

    public int CustomerId => ViewModel.Customer.Id;

    public CustomerEditorView(Customer? customer)
    {
        InitializeComponent();
        DataContext = new CustomerEditorViewModel(customer);
        ShowAddress();

        var saveButton = EditorWorkflowSupport.AddGridViewToggle(this, GridView_Click);
        _dirtyMonitor = new EditorDirtyMonitor(saveButton, CaptureEditState);
        AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(_dirtyMonitor.Start, DispatcherPriority.Loaded);
    }

    public void StopTracking() => _dirtyMonitor.Dispose();

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
    private void QuoteHistory_Click(object? sender, RoutedEventArgs e) => ShowHistory("Quote History");
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
    }

    private void ShowContacts()
    {
        AddressPanel.IsVisible = false;
        ContactPanel.IsVisible = true;
        HistoryPanel.IsVisible = false;
    }

    private void ShowHistory(string title)
    {
        AddressPanel.IsVisible = false;
        ContactPanel.IsVisible = false;
        HistoryPanel.IsVisible = true;
        HistoryTitle.Text = title;
    }
}
