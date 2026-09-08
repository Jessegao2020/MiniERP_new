using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Invoices;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Invoices;

public partial class InvoiceListView : UserControl
{
    private readonly InvoiceType _type;
    private InvoiceListViewModel ViewModel => (InvoiceListViewModel)DataContext!;

    public event Action<Invoice?>? OpenInvoiceRequested;

    public InvoiceListView(InvoiceType type)
    {
        _type = type;
        InitializeComponent();
        DataContext = new InvoiceListViewModel(type);
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadAsync();
    }

    public Task ReloadAsync() => ViewModel.LoadAsync();
    public InvoiceType Type => _type;

    private void New_Click(object? sender, RoutedEventArgs e) => OpenInvoiceRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedInvoice is null) { await ViewModel.DeleteSelectedAsync(); return; }
        var confirmed = await ConfirmationDialog.ShowAsync(this, $"Delete {ViewModel.DocumentTitle}", $"Delete '{ViewModel.SelectedInvoice.InvoiceNumber}'? This cannot be undone.");
        if (confirmed) await ViewModel.DeleteSelectedAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is string field) ViewModel.SetFilter(field, textBox.Text);
    }

    private void InvoiceGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedInvoice is not null) OpenInvoiceRequested?.Invoke(ViewModel.SelectedInvoice);
    }
}
