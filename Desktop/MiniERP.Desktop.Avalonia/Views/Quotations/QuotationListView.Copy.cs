using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationListView
{
    // Duplicate is declared directly in the list toolbar XAML so it shares
    // the Article/Quotation compact styling and cannot be inserted twice.

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedQuotation;
        if (selected is null)
        {
            ViewModel.SetStatusMessage("Select a quotation to duplicate.");
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            var source = await service.GetQuotationByIdAsync(selected.Id);
            if (source is null)
            {
                ViewModel.SetStatusMessage("The selected quotation no longer exists.");
                return;
            }

            if (source.User is null)
            {
                ViewModel.SetStatusMessage("The source quotation user could not be loaded.");
                return;
            }

            var copy = QuotationCopySupport.CreateCopy(source);
            await QuotationNumberSupport.CreateAsync(service, copy, source.User.Code);
            OpenQuotationRequested?.Invoke(copy);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatusMessage($"Duplicate failed: {ex.Message}");
        }
    }
}
