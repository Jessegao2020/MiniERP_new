using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Infrastructure;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationListView
{
    private bool _copyButtonInstalled;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InstallCopyButton();
    }

    private void InstallCopyButton()
    {
        if (_copyButtonInstalled || Content is not Grid root)
            return;

        var toolbar = root.Children
            .OfType<StackPanel>()
            .FirstOrDefault(panel => panel.Children
                .OfType<Button>()
                .Any(button => string.Equals(button.Content as string, "New", StringComparison.Ordinal)));

        if (toolbar is null)
            return;

        var copyButton = new Button
        {
            Padding = new Thickness(8, 2),
            Content = "Copy"
        };
        copyButton.Click += Copy_Click;

        var newButtonIndex = toolbar.Children
            .Select((child, index) => (child, index))
            .FirstOrDefault(pair => pair.child is Button button
                && string.Equals(button.Content as string, "New", StringComparison.Ordinal))
            .index;

        toolbar.Children.Insert(Math.Min(newButtonIndex + 1, toolbar.Children.Count), copyButton);
        _copyButtonInstalled = true;
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedQuotation;
        if (selected is null)
        {
            ViewModel.SetStatusMessage("Select a quotation to copy.");
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

            var copy = QuotationCopySupport.CreateCopy(source);
            await service.CreateQuotationAsync(copy);
            OpenQuotationRequested?.Invoke(copy);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatusMessage($"Copy failed: {ex.Message}");
        }
    }
}
