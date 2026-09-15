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
                        Text = "Copy",
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
            ViewModel.SetStatusMessage("Save or discard the current position changes before copying the quotation.");
            return;
        }

        if (ViewModel.SelectedCustomer is null)
        {
            ViewModel.SetStatusMessage("Select a customer before copying the quotation.");
            return;
        }

        if (ViewModel.SelectedUser is null)
        {
            ViewModel.SetStatusMessage("Select a user before copying the quotation.");
            return;
        }

        try
        {
            var snapshot = BuildCurrentQuotationSnapshotForCopy();
            var copy = QuotationCopySupport.CreateCopy(snapshot);

            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();
            await service.CreateQuotationAsync(copy);

            CopyCreated?.Invoke(copy);
        }
        catch (Exception ex)
        {
            ViewModel.SetStatusMessage($"Copy failed: {ex.Message}");
        }
    }

    private Quotation BuildCurrentQuotationSnapshotForCopy()
    {
        var quotation = ViewModel.Quotation;
        var customer = ViewModel.SelectedCustomer!;
        var user = ViewModel.SelectedUser!;
        var contact = ViewModel.SelectedCustomerContact;

        return new Quotation
        {
            QuotationNumber = quotation.QuotationNumber,
            CustomerId = customer.Id,
            UserId = user.Id,
            DeliveryTerm = quotation.DeliveryTerm,
            LeadTime = quotation.LeadTime,
            PaymentTerm = quotation.PaymentTerm,
            Remarks = quotation.Remarks,
            QuotationDate = ViewModel.QuotationDate?.DateTime ?? quotation.QuotationDate,
            ValidUntil = ViewModel.ValidUntil?.DateTime,
            Currency = ViewModel.Currency,
            ExchangeRate = ViewModel.ExchangeRateSnapshot,
            CustomerNameSnapshot = customer.Name.Trim(),
            CustomerAddressSnapshot = BuildCopyCustomerAddress(customer),
            CustomerContactSnapshot = contact is null
                ? customer.Id == quotation.CustomerId ? quotation.CustomerContactSnapshot : null
                : BuildCopyContactName(contact),
            SalesContactNameSnapshot = user.Name.Trim(),
            SalesContactPhoneSnapshot = user.Phone,
            SalesContactEmailSnapshot = user.Email,
            Items = ViewModel.Items.Select((item, index) => item.ToEntity(index)).ToList()
        };
    }

    private static string BuildCopyContactName(CustomerContact contact)
    {
        var title = contact.Title?.Trim();
        var name = contact.Name.Trim();
        return string.IsNullOrWhiteSpace(title) ? name : $"{title} {name}".Trim();
    }

    private static string? BuildCopyCustomerAddress(Customer customer)
    {
        var lines = new List<string>();
        AddCopyAddressLine(lines, customer.AddressLine1);
        AddCopyAddressLine(lines, customer.AddressLine2);

        var cityLine = string.Join(" ", new[]
        {
            customer.PostalCode?.Trim(),
            customer.City?.Trim(),
            customer.State?.Trim()
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        AddCopyAddressLine(lines, cityLine);
        AddCopyAddressLine(lines, CountryRegionNames.GetDisplayName(customer.Country));

        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static void AddCopyAddressLine(ICollection<string> lines, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add(value.Trim());
    }
}
