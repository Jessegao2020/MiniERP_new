using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniERP.Desktop.ViewModels.Customers;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Customers;

public partial class CustomerEditorView : UserControl
{
    private CustomerEditorViewModel ViewModel => (CustomerEditorViewModel)DataContext!;

    public event EventHandler? Saved;
    public event EventHandler? Deleted;
    public event EventHandler? RequestClose;

    public CustomerEditorView(Customer? customer)
    {
        InitializeComponent();
        DataContext = new CustomerEditorViewModel(customer);
        ShowAddress();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.SaveAsync())
            return;

        Saved?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void Discard_Click(object? sender, RoutedEventArgs e)
        => RequestClose?.Invoke(this, EventArgs.Empty);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ViewModel.DeleteAsync())
            return;

        Deleted?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void Address_Click(object? sender, RoutedEventArgs e)
        => ShowAddress();

    private void Contact_Click(object? sender, RoutedEventArgs e)
        => ShowContacts();

    private void QuoteHistory_Click(object? sender, RoutedEventArgs e)
        => ShowHistory("Quote History");

    private void OrderHistory_Click(object? sender, RoutedEventArgs e)
        => ShowHistory("Order History");

    private void NewContact_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddContact();
        ShowContacts();
    }

    private void DeleteContact_Click(object? sender, RoutedEventArgs e)
        => ViewModel.DeleteSelectedContact();

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
