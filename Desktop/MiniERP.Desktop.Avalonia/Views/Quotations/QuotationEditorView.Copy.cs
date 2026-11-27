using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Desktop.Infrastructure;
using MiniERP.Domain;

namespace MiniERP.Desktop.Views.Quotations;

public partial class QuotationEditorView
{
    private bool _quotationCopyButtonInstalled;

    public event Action<Quotation>? CopyCreated;

    private void InstallQuotationCopyButton()
    {
        if (_quotationCopyButtonInstalled || Content is not Grid root)
            return;

        var toolbar = root.Children
            .OfType<WrapPanel>()
            .FirstOrDefault(panel => panel.Children.OfType<Button>().Any(IsExportPdfButton));
        if (toolbar is null)
            return;

        var exportButton = toolbar.Children.OfType<Button>().FirstOrDefault(IsExportPdfButton);
        if (exportButton is null)
            return;

        var copyButton = new Button
        {
            Padding = new Thickness(6, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "⧉" },
                    new TextBlock
                    {
                        Text = "Duplicate",
                        FontSize = 10,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        copyButton.Click += CopyQuotation_Click;

        var exportIndex = toolbar.Children.IndexOf(exportButton);
        toolbar.Children.Insert(exportIndex, copyButton);
        _quotationCopyButtonInstalled = true;
    }

    private static bool IsExportPdfButton(Button button)
        => button.Content is StackPanel content
           && content.Children.OfType<TextBlock>().Any(text => string.Equals(text.Text, "Export PDF", StringComparison.Ordinal));

    private async void CopyQuotation_Click(object? sender, RoutedEventArgs e)
    {
        if (HasMeaningfulPositionEditorChanges())
        {
            ViewModel.SetStatusMessage("Save or discard the current position changes before duplicating the quotation.");
            return;
        }

        if (ViewModel.SelectedCustomer is null)
        {
            ViewModel.SetStatusMessage("Select a customer before duplicating the quotation.");
            return;
        }

        if (ViewModel.SelectedUser is null)
        {
            ViewModel.SetStatusMessage("Select a user before duplicating the quotation.");
            return;
        }

        try
        {
            var snapshot = BuildCurrentQuotationSnapshotForCopy();
            var copy = QuotationCopySupport.CreateCopy(snapshot);

            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            await QuotationNumberSupport.CreateAsync(service, copy, ViewModel.SelectedUser!.Code);

            CopyCreated?.Invoke(copy);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatusMessage($"Duplicate failed: {ex.Message}");
        }
    }

    private Quotation BuildCurrentQuotationSnapshotForCopy()
    {
        var quotation = ViewModel.Quotation;
        var customer = ViewModel.SelectedCustomer!;
        var user = ViewModel.SelectedUser!;
        var contact = ViewModel.SelectedCustomerContact;

        return QuotationRules.BuildCopySnapshot(quotation, customer, user, contact,
            ViewModel.QuotationDate?.DateTime ?? quotation.QuotationDate, ViewModel.ValidUntil?.DateTime,
            ViewModel.Currency, ViewModel.ExchangeRateSnapshot,
            ViewModel.Items.Select((item, index) => item.ToEntity(index)).ToList(),
            CountryRegionNames.GetDisplayName(customer.Country));
    }

}
