using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Desktop.ViewModels.Quotations;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationListView : UserControl
{
    private QuotationListViewModel ViewModel => (QuotationListViewModel)DataContext!;

    public event Action<Quotation?>? OpenQuotationRequested;

    public QuotationListView()
    {
        InitializeComponent();
        DataContext = new QuotationListViewModel();
        AttachedToVisualTree += async (_, _) => await ViewModel.LoadAsync();
    }

    public Task ReloadAsync() => ViewModel.LoadAsync();

    private void New_Click(object? sender, RoutedEventArgs e)
        => OpenQuotationRequested?.Invoke(null);

    private async void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuotation is null)
        {
            await ViewModel.DeleteSelectedAsync();
            return;
        }

        var confirmed = await ConfirmationDialog.ShowAsync(
            this,
            "Delete Quotation",
            $"Delete quotation '{ViewModel.SelectedQuotation.QuotationNumber}'? This cannot be undone.");

        if (confirmed)
            await ViewModel.DeleteSelectedAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.LoadAsync();

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string field)
            return;

        ViewModel.SetFilter(field, textBox.Text);
    }

    private void QuotationGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedQuotation is not null)
            OpenQuotationRequested?.Invoke(ViewModel.SelectedQuotation);
    }
}
