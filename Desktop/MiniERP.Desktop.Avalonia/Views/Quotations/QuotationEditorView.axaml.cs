using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private void AddItem_Click(object? sender, RoutedEventArgs e)
        => ViewModel.AddSelectedArticle();

    private void RemoveItem_Click(object? sender, RoutedEventArgs e)
        => ViewModel.RemoveSelectedItem();

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
        if (!ViewModel.IsNew)
        {
            var confirmed = await ConfirmationDialog.ShowAsync(
                this,
                "Delete Quotation",
                $"Delete quotation '{ViewModel.Quotation.QuotationNumber}'? This cannot be undone.");

            if (!confirmed)
                return;
        }

        if (!await ViewModel.DeleteAsync())
            return;

        Deleted?.Invoke(this, EventArgs.Empty);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
