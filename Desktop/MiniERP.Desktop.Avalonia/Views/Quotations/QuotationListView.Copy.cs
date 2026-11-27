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
            var workflow = scope.ServiceProvider.GetRequiredService<IQuotationWorkflow>();
            var detail = await workflow.DuplicateAsync(selected.Id);
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            var copy = await service.GetQuotationByIdAsync(detail.Summary.Id)
                ?? throw new InvalidOperationException("The copied quotation could not be loaded.");
            OpenQuotationRequested?.Invoke(copy);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatusMessage($"Duplicate failed: {ex.Message}");
        }
    }
}
